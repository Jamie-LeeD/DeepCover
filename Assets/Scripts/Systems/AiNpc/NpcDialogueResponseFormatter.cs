/// <summary>
/// Post-processes raw model output (separate from prompt construction).
/// </summary>
public static class NpcDialogueResponseFormatter
{
    private const int DefaultMaxSentences = 3;

    /// <summary>
    /// Trims model output to at most N sentence-ending boundaries to match UI / design caps.
    /// </summary>
    public static string ClampToMaxSentences(string text, int maxSentences = DefaultMaxSentences)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = text.Trim();
        if (maxSentences <= 0)
        {
            return text;
        }

        int sentences = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c != '.' && c != '!' && c != '?')
            {
                continue;
            }

            bool atEnd = i == text.Length - 1;
            bool followedByBreak = !atEnd && (char.IsWhiteSpace(text[i + 1]) || text[i + 1] == '"');
            if (!atEnd && !followedByBreak)
            {
                continue;
            }

            sentences++;
            if (sentences >= maxSentences)
            {
                return text.Substring(0, i + 1).Trim();
            }
        }

        return text;
    }
}
