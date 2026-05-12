using System;
using System.Collections.Generic;

/// <summary>
/// Supplies dialogue content from AI systems or other runtime generators.
/// </summary>
public interface IDialogueContentProvider
{
    string SpeakerName { get; }
    bool TryGetDialogue(out DialogueSequence sequence);
}

/// <summary>
/// Builds dialogue lines at runtime before a conversation starts.
/// </summary>
public interface IRuntimeDialogueBuilder
{
    void BuildDialogue(List<DialogueLineData> lines);
}
