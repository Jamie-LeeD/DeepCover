using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Short-term conversational memory for one NPC brain instance.
/// </summary>
public class NPCMemory
{
    private readonly List<MemoryTurn> turns = new List<MemoryTurn>();
    private readonly List<string> summaryNotes = new List<string>();
    private readonly int maxTurns;
    private readonly int maxSummaryNotes;

    public NPCMemory(int maxTurns, int maxSummaryNotes = 8)
    {
        this.maxTurns = Math.Max(1, maxTurns);
        this.maxSummaryNotes = Math.Max(0, maxSummaryNotes);
    }

    public void Clear()
    {
        turns.Clear();
        summaryNotes.Clear();
    }

    /// <summary>
    /// Optional long-lived facts (key leads, alibis) prepended to the transcript block for future prompts.
    /// </summary>
    public void AddSummaryNote(string note)
    {
        if (string.IsNullOrWhiteSpace(note) || maxSummaryNotes <= 0)
        {
            return;
        }

        summaryNotes.Add(note.Trim());
        while (summaryNotes.Count > maxSummaryNotes)
        {
            summaryNotes.RemoveAt(0);
        }
    }

    public void AddPlayerUtterance(string text)
    {
        AddTurn(true, text);
    }

    public void AddNpcReply(string text)
    {
        AddTurn(false, text);
    }

    private void AddTurn(bool fromPlayer, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        turns.Add(new MemoryTurn(fromPlayer, text.Trim()));
        while (turns.Count > maxTurns)
        {
            turns.RemoveAt(0);
        }
    }

    public string BuildTranscript()
    {
        StringBuilder builder = new StringBuilder();

        if (summaryNotes.Count > 0)
        {
            builder.AppendLine("## Case notes (designer / AI summarised):");
            for (int i = 0; i < summaryNotes.Count; i++)
            {
                builder.Append("- ").AppendLine(summaryNotes[i]);
            }

            builder.AppendLine();
        }

        if (turns.Count == 0)
        {
            builder.Append("(No prior lines in this conversation.)");
            return builder.ToString().TrimEnd();
        }

        for (int i = 0; i < turns.Count; i++)
        {
            MemoryTurn turn = turns[i];
            builder.Append(turn.FromPlayer ? "Investigator: " : "Character: ");
            builder.AppendLine(turn.Text);
        }

        return builder.ToString().TrimEnd();
    }

    private readonly struct MemoryTurn
    {
        public readonly bool FromPlayer;
        public readonly string Text;

        public MemoryTurn(bool fromPlayer, string text)
        {
            FromPlayer = fromPlayer;
            Text = text;
        }
    }
}
