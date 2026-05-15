using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// HTTP client for a local Ollama server. Use from coroutines via <see cref="GenerateAsync"/> or <see cref="GenerateStreamAsync"/>.
/// </summary>
public class OllamaClient
{
    private const string GeneratePath = "/api/generate";
    private readonly List<string> deltaScratch = new List<string>(8);

    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3";
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Runs a non-streaming generate request (user prompt only).
    /// </summary>
    public IEnumerator GenerateAsync(string prompt, Action<OllamaResult> onComplete)
    {
        IEnumerator nested = GenerateAsync(prompt, null, onComplete);
        while (nested.MoveNext())
        {
            yield return nested.Current;
        }
    }

    /// <summary>
    /// Runs a non-streaming generate request with an optional system prompt (character / rules).
    /// </summary>
    public IEnumerator GenerateAsync(string userPrompt, string systemPrompt, Action<OllamaResult> onComplete)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            onComplete?.Invoke(OllamaResult.Fail("Prompt is empty."));
            yield break;
        }

        string url = BuildGenerateUrl();
        string jsonBody = OllamaGenerateRequest.ToJson(Model, userPrompt, systemPrompt, stream: false);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = Mathf.Max(1, TimeoutSeconds);

            yield return request.SendWebRequest();

            OllamaResult result = BuildResult(request, Model);
            onComplete?.Invoke(result);
        }
    }

    /// <summary>
    /// Streams partial tokens via <paramref name="onDelta"/> as they arrive (NDJSON).
    /// Invoke <paramref name="onComplete"/> once with the full assembled text or an error.
    /// </summary>
    public IEnumerator GenerateStreamAsync(
        string userPrompt,
        string systemPrompt,
        Action<string> onDelta,
        Action<OllamaResult> onComplete,
        OllamaCancelToken cancellation = null)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            onComplete?.Invoke(OllamaResult.Fail("Prompt is empty."));
            yield break;
        }

        if (cancellation != null && cancellation.IsCancellationRequested)
        {
            onComplete?.Invoke(OllamaResult.Fail("Cancelled."));
            yield break;
        }

        string url = BuildGenerateUrl();
        string jsonBody = OllamaGenerateRequest.ToJson(Model, userPrompt, systemPrompt, stream: true);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        OllamaStreamingDownloadHandler streamHandler = new OllamaStreamingDownloadHandler();
        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = streamHandler;
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = Mathf.Max(1, TimeoutSeconds);

            cancellation?.Bind(request);
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                if (cancellation != null && cancellation.IsCancellationRequested)
                {
                    request.Abort();
                    onComplete?.Invoke(OllamaResult.Fail("Cancelled."));
                    yield break;
                }

                EmitPendingDeltas(streamHandler, onDelta);
                yield return null;
            }

            cancellation?.Unbind();
            EmitPendingDeltas(streamHandler, onDelta);

            if (cancellation != null && cancellation.IsCancellationRequested)
            {
                onComplete?.Invoke(OllamaResult.Fail("Cancelled."));
                yield break;
            }

            OllamaResult result = BuildStreamResult(request, streamHandler, Model);
            onComplete?.Invoke(result);
        }
    }

    private void EmitPendingDeltas(OllamaStreamingDownloadHandler handler, Action<string> onDelta)
    {
        if (handler == null || onDelta == null)
        {
            return;
        }

        deltaScratch.Clear();
        handler.DrainPendingDeltas(deltaScratch);
        for (int i = 0; i < deltaScratch.Count; i++)
        {
            onDelta(deltaScratch[i]);
        }
    }

    private static OllamaResult BuildStreamResult(
        UnityWebRequest request,
        OllamaStreamingDownloadHandler handler,
        string configuredModel)
    {
        if (request.result == UnityWebRequest.Result.ConnectionError)
        {
            return OllamaResult.Fail("Connection failed. Is Ollama running? (" + request.error + ")");
        }

        if (request.result == UnityWebRequest.Result.DataProcessingError)
        {
            return OllamaResult.Fail("Data processing error: " + request.error);
        }

        if (request.result == UnityWebRequest.Result.ProtocolError)
        {
            return BuildProtocolErrorResult(request, configuredModel);
        }

        string text = handler?.FullText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return OllamaResult.Fail("Empty streamed response from Ollama.");
        }

        return OllamaResult.Ok(text);
    }

    private string BuildGenerateUrl()
    {
        string baseUrl = (BaseUrl ?? string.Empty).TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "http://localhost:11434";
        }

        return baseUrl + GeneratePath;
    }

    private static OllamaResult BuildResult(UnityWebRequest request, string configuredModel)
    {
        if (request.result == UnityWebRequest.Result.ConnectionError)
        {
            return OllamaResult.Fail(
                "Connection failed. Is Ollama running? (" + request.error + ")");
        }

        if (request.result == UnityWebRequest.Result.DataProcessingError)
        {
            return OllamaResult.Fail("Data processing error: " + request.error);
        }

        if (request.result == UnityWebRequest.Result.ProtocolError)
        {
            return BuildProtocolErrorResult(request, configuredModel);
        }

        string raw = request.downloadHandler?.text;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return OllamaResult.Fail("Empty response body from Ollama.");
        }

        try
        {
            OllamaGenerateResponse parsed = JsonUtility.FromJson<OllamaGenerateResponse>(raw);
            if (parsed == null)
            {
                return OllamaResult.Fail("Failed to parse JSON (null result).");
            }

            if (string.IsNullOrEmpty(parsed.response))
            {
                return OllamaResult.Fail(
                    "Parsed JSON but 'response' field was empty. Raw: " + Truncate(raw, 300));
            }

            return OllamaResult.Ok(parsed.response);
        }
        catch (Exception exception)
        {
            return OllamaResult.Fail("Invalid JSON: " + exception.Message + ". Raw: " + Truncate(raw, 300));
        }
    }

    private static OllamaResult BuildProtocolErrorResult(UnityWebRequest request, string configuredModel)
    {
        string body = request.downloadHandler?.text ?? string.Empty;
        string ollamaMessage = TryParseOllamaError(body);

        if (request.responseCode == 404 && !string.IsNullOrEmpty(ollamaMessage))
        {
            string hint =
                "\n\nFix: the Model name must match an entry from `ollama list` exactly (including tags, e.g. llama3.2:3b). " +
                "Install this model: `ollama pull " + (configuredModel ?? "MODEL_NAME") + "` " +
                "or change the Model field in the inspector to a model you already pulled.";
            return OllamaResult.Fail($"HTTP 404 — {ollamaMessage}.{hint}");
        }

        return OllamaResult.Fail(
            $"HTTP {(int)request.responseCode} {request.error}. Body: {Truncate(body, 500)}");
    }

    private static string TryParseOllamaError(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            OllamaErrorResponse parsed = JsonUtility.FromJson<OllamaErrorResponse>(json);
            if (parsed != null && !string.IsNullOrEmpty(parsed.error))
            {
                return parsed.error;
            }
        }
        catch (Exception)
        {
            // ignore; fall back to raw body in caller
        }

        return null;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength) + "...";
    }
}
