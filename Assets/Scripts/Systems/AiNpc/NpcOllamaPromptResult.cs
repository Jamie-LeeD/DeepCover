using System.Text;

/// <summary>
/// Result of assembling Ollama prompts: split system/user for the API, plus debug metadata.
/// </summary>
public readonly struct NpcOllamaPromptResult
{
    public string SystemPrompt { get; }
    public string UserPrompt { get; }
    public int SystemCharacterCount { get; }
    public int UserCharacterCount { get; }
    public int RoughTokenEstimate { get; }

    public NpcOllamaPromptResult(string systemPrompt, string userPrompt)
    {
        SystemPrompt = systemPrompt ?? string.Empty;
        UserPrompt = userPrompt ?? string.Empty;
        SystemCharacterCount = SystemPrompt.Length;
        UserCharacterCount = UserPrompt.Length;
        RoughTokenEstimate = (SystemCharacterCount + UserCharacterCount + 3) / 4;
    }

    /// <summary>
    /// Full system + user prompt in one string for logs, tests, or clipboard export.
    /// Ollama should still receive <see cref="SystemPrompt"/> and <see cref="UserPrompt"/> separately for best instruction following.
    /// </summary>
    public string GetFormattedPromptString()
    {
        return BuildCombinedDebugView();
    }

    /// <summary>
    /// Single labeled blob for logging / inspector paste.
    /// </summary>
    public string BuildCombinedDebugView()
    {
        StringBuilder builder = new StringBuilder(SystemCharacterCount + UserCharacterCount + 64);
        builder.AppendLine("=== SYSTEM ===");
        builder.AppendLine(SystemPrompt);
        builder.AppendLine();
        builder.AppendLine("=== USER ===");
        builder.AppendLine(UserPrompt);
        builder.AppendLine();
        builder.Append("Chars: system=").Append(SystemCharacterCount)
            .Append(", user=").Append(UserCharacterCount)
            .Append(", ~tokens≈").Append(RoughTokenEstimate);
        return builder.ToString();
    }

    public string BuildCompactDebugSummary()
    {
        return $"[NpcOllamaPrompt] sys={SystemCharacterCount}c user={UserCharacterCount}c ~tok≈{RoughTokenEstimate}";
    }
}
