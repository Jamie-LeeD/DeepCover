using System;
using UnityEngine;

/// <summary>
/// JSON body for Ollama POST /api/generate (non-streaming).
/// </summary>
[Serializable]
public class OllamaGenerateRequest
{
    public string model;
    public string prompt;
    public string system;
    public bool stream;

    public static string ToJson(string model, string prompt, bool stream = false)
    {
        return ToJson(model, prompt, null, stream);
    }

    public static string ToJson(string model, string prompt, string system, bool stream)
    {
        OllamaGenerateRequest body = new OllamaGenerateRequest
        {
            model = model,
            prompt = prompt,
            stream = stream
        };

        if (!string.IsNullOrWhiteSpace(system))
        {
            body.system = system;
        }

        return JsonUtility.ToJson(body);
    }
}

/// <summary>
/// Subset of Ollama generate response fields we deserialize from JSON.
/// </summary>
[Serializable]
public class OllamaGenerateResponse
{
    public string model;
    public string created_at;
    public string response;
    public bool done;
}

/// <summary>
/// Error JSON returned by Ollama on failed requests (e.g. unknown model).
/// </summary>
[Serializable]
public class OllamaErrorResponse
{
    public string error;
}

/// <summary>
/// Outcome of a single generate call.
/// </summary>
public readonly struct OllamaResult
{
    public bool Success { get; }
    public string Text { get; }
    public string ErrorMessage { get; }

    public OllamaResult(bool success, string text, string errorMessage)
    {
        Success = success;
        Text = text ?? string.Empty;
        ErrorMessage = errorMessage ?? string.Empty;
    }

    public static OllamaResult Ok(string text) => new OllamaResult(true, text, string.Empty);

    public static OllamaResult Fail(string errorMessage) => new OllamaResult(false, string.Empty, errorMessage);
}
