using System.Collections;
using UnityEngine;

/// <summary>
/// Example MonoBehaviour that sends a prompt to local Ollama and logs the result (for local testing).
/// </summary>
public class OllamaTestRunner : MonoBehaviour
{
    [Header("Ollama")]
    [SerializeField] private string baseUrl = "http://localhost:11434";
    [Tooltip("Must match a name from `ollama list` exactly (e.g. llama3, llama3.2:3b, mistral).")]
    [SerializeField] private string model = "llama3";
    [SerializeField] private int timeoutSeconds = 120;

    [Header("Test")]
    [TextArea(2, 8)]
    [SerializeField] private string testPrompt = "Reply in one short sentence: what is 2+2?";
    [SerializeField] private bool runOnStart = true;
    [SerializeField] private bool useStreaming = true;

    private OllamaClient client;

    private void Awake()
    {
        client = new OllamaClient
        {
            BaseUrl = baseUrl,
            Model = model,
            TimeoutSeconds = timeoutSeconds
        };
    }

    private void Start()
    {
        if (runOnStart)
        {
            StartCoroutine(RunTest());
        }
    }

    /// <summary>
    /// Call from UI button or other code to re-run the test prompt.
    /// </summary>
    public void RunTestFromInspector()
    {
        StopAllCoroutines();
        StartCoroutine(RunTest());
    }

    private IEnumerator RunTest()
    {
        client.BaseUrl = baseUrl;
        client.Model = model;
        client.TimeoutSeconds = timeoutSeconds;

        if (useStreaming)
        {
            OllamaCancelToken cancel = new OllamaCancelToken();
            System.Text.StringBuilder live = new System.Text.StringBuilder();
            OllamaResult result = default;

            IEnumerator streamRoutine = client.GenerateStreamAsync(
                testPrompt,
                null,
                delta =>
                {
                    live.Append(delta);
                    Debug.Log($"[Ollama Stream] +{delta}", this);
                },
                r => { result = r; },
                cancel);

            while (streamRoutine.MoveNext())
            {
                yield return streamRoutine.Current;
            }

            if (result.Success)
            {
                Debug.Log($"[Ollama Stream] Done model={model}\n{result.Text}", this);
            }
            else
            {
                Debug.LogWarning($"[Ollama Stream] Error: {result.ErrorMessage}", this);
            }

            yield break;
        }

        OllamaResult blockingResult = default;
        IEnumerator requestRoutine = client.GenerateAsync(testPrompt, r => { blockingResult = r; });

        while (requestRoutine.MoveNext())
        {
            yield return requestRoutine.Current;
        }

        if (blockingResult.Success)
        {
            Debug.Log($"[Ollama] Model={model}\n{blockingResult.Text}", this);
        }
        else
        {
            Debug.LogWarning($"[Ollama] Error: {blockingResult.ErrorMessage}", this);
        }
    }
}
