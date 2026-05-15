using UnityEngine.Networking;

/// <summary>
/// Shared cancellation handle for in-flight Ollama requests (ESC, dialogue close, scene unload).
/// </summary>
public sealed class OllamaCancelToken
{
    public bool IsCancellationRequested { get; private set; }

    private UnityWebRequest boundRequest;

    public void Cancel()
    {
        if (IsCancellationRequested)
        {
            return;
        }

        IsCancellationRequested = true;
        boundRequest?.Abort();
    }

    internal void Bind(UnityWebRequest request)
    {
        boundRequest = request;
    }

    internal void Unbind()
    {
        boundRequest = null;
    }
}
