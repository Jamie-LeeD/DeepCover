using System.Collections.Generic;

/// <summary>
/// Future hook for choice-based or branching dialogue feeding lines into <see cref="NPCBrain"/>.
/// Implement on a UI or quest object and read from <see cref="NPCBrain"/> when you add branching.
/// </summary>
public interface IDialogueBranchingDialogue
{
    /// <summary>
    /// Optional labels for player choices (empty until a branching UI exists).
    /// </summary>
    IReadOnlyList<string> GetPendingChoiceLabels();

    /// <summary>
    /// Dequeues the player's chosen line for the next AI turn, if any.
    /// </summary>
    bool TryConsumePlayerChoice(out string choiceLabel);
}
