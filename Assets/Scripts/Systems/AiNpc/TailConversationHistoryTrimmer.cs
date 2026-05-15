using System;
using System.Text;

/// <summary>
/// Default trimmer: keeps the most recent lines (tail) so the model sees latest intent, not old setup.
/// Dropping the head is standard for chat context windows when you cannot afford full history.
/// </summary>
public sealed class TailConversationHistoryTrimmer : IConversationHistoryTrimmer
{
    public string TrimForPrompt(string fullTranscript, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(fullTranscript))
        {
            return string.Empty;
        }

        string trimmed = fullTranscript.Trim();
        if (trimmed.Length <= maxCharacters)
        {
            return trimmed;
        }

        // Prefer line boundaries so we do not cut mid-line at the front when possible.
        int start = 0;
        while (trimmed.Length - start > maxCharacters)
        {
            int nextNewline = trimmed.IndexOf('\n', start);
            if (nextNewline < 0 || nextNewline == start)
            {
                start = Math.Min(start + 80, trimmed.Length - 1);
                break;
            }

            start = nextNewline + 1;
        }

        string tail = trimmed.Substring(start).Trim();
        if (tail.Length <= maxCharacters)
        {
            return tail;
        }

        return PromptTextUtilities.TruncateEnd(tail, maxCharacters);
    }
}
