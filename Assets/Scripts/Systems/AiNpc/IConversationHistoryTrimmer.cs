/// <summary>
/// Pluggable strategy to shrink conversation history for token-conscious prompts.
/// Swap implementations later (e.g. summarization, semantic recall) without changing the builder.
/// </summary>
public interface IConversationHistoryTrimmer
{
    /// <summary>
    /// Returns a transcript no longer than <paramref name="maxCharacters"/> (best effort).
    /// </summary>
    string TrimForPrompt(string fullTranscript, int maxCharacters);
}
