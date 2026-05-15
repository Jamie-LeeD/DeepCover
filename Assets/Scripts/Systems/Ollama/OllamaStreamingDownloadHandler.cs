using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Receives NDJSON chunks from Ollama <c>stream: true</c> responses on a background thread.
/// Main thread drains deltas via <see cref="DrainPendingDeltas"/>.
/// </summary>
public sealed class OllamaStreamingDownloadHandler : DownloadHandlerScript
{
    private readonly object sync = new object();
    private readonly StringBuilder lineBuffer = new StringBuilder(256);
    private readonly Queue<string> pendingDeltas = new Queue<string>(32);
    private readonly StringBuilder fullResponse = new StringBuilder(512);

    public bool StreamCompleted { get; private set; }
    public string FullText => fullResponse.ToString();

    public OllamaStreamingDownloadHandler()
        : base(new byte[16 * 1024])
    {
    }

    protected override bool ReceiveData(byte[] data, int dataLength)
    {
        if (data == null || dataLength <= 0)
        {
            return true;
        }

        string chunk = Encoding.UTF8.GetString(data, 0, dataLength);
        lock (sync)
        {
            lineBuffer.Append(chunk);
            ParseCompleteLinesLocked();
        }

        return true;
    }

    protected override void CompleteContent()
    {
        lock (sync)
        {
            FlushTrailingLineLocked();
            StreamCompleted = true;
        }
    }

    /// <summary>Drains token deltas queued since the last call (call from main thread only).</summary>
    public void DrainPendingDeltas(List<string> destination)
    {
        if (destination == null)
        {
            return;
        }

        lock (sync)
        {
            while (pendingDeltas.Count > 0)
            {
                destination.Add(pendingDeltas.Dequeue());
            }
        }
    }

    private void ParseCompleteLinesLocked()
    {
        while (TryExtractLineLocked(out string line))
        {
            ProcessJsonLineLocked(line);
        }
    }

    private void FlushTrailingLineLocked()
    {
        if (lineBuffer.Length == 0)
        {
            return;
        }

        string line = lineBuffer.ToString().Trim();
        lineBuffer.Clear();
        if (line.Length > 0)
        {
            ProcessJsonLineLocked(line);
        }
    }

    private bool TryExtractLineLocked(out string line)
    {
        line = null;
        for (int i = 0; i < lineBuffer.Length; i++)
        {
            if (lineBuffer[i] != '\n')
            {
                continue;
            }

            line = lineBuffer.ToString(0, i).Trim('\r', ' ');
            lineBuffer.Remove(0, i + 1);
            return line.Length > 0;
        }

        return false;
    }

    private void ProcessJsonLineLocked(string jsonLine)
    {
        if (string.IsNullOrWhiteSpace(jsonLine))
        {
            return;
        }

        try
        {
            OllamaGenerateResponse parsed = JsonUtility.FromJson<OllamaGenerateResponse>(jsonLine);
            if (parsed == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(parsed.response))
            {
                pendingDeltas.Enqueue(parsed.response);
                fullResponse.Append(parsed.response);
            }

            if (parsed.done)
            {
                StreamCompleted = true;
            }
        }
        catch
        {
            // Skip malformed partial lines; stream may still recover.
        }
    }
}
