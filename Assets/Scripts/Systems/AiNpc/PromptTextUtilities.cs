using System.Text;

/// <summary>
/// Shared truncation helpers for prompt sections (evidence, secrets, etc.).
/// </summary>
public static class PromptTextUtilities
{
    public static string TruncateEnd(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
        {
            return text ?? string.Empty;
        }

        if (maxChars <= 1)
        {
            return "…";
        }

        return text.Substring(0, maxChars - 1).TrimEnd() + "…";
    }

    /// <summary>
    /// Collapses repeated blank lines so prompts stay readable without wasted tokens.
    /// </summary>
    public static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(text.Length);
        bool lastWasNewline = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r')
            {
                continue;
            }

            if (c == '\n')
            {
                if (!lastWasNewline)
                {
                    builder.Append('\n');
                    lastWasNewline = true;
                }

                continue;
            }

            lastWasNewline = false;
            builder.Append(c);
        }

        return builder.ToString().Trim();
    }
}
