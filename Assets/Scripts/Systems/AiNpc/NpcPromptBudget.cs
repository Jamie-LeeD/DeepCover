using System;
using UnityEngine;

/// <summary>
/// Per-section character budgets to control token use and prompt bloat.
/// Rough rule of thumb: ~4 characters ≈ 1 English token (very approximate).
/// </summary>
[Serializable]
public class NpcPromptBudget
{
    [Header("System prompt")]
    [Tooltip("Personality + attitude text cap. Keeps system prompt from dominating context.")]
    [Min(80)]
    public int maxPersonalityChars = 600;

    [Tooltip("Voice / delivery guidelines cap.")]
    [Min(40)]
    public int maxVoiceGuidelinesChars = 280;

    [Tooltip("Private knowledge for the model only; long secrets blow up tokens fast.")]
    [Min(80)]
    public int maxSecretsChars = 520;

    [Header("User prompt")]
    [Tooltip("Evidence summary from inventory; cap prevents huge item lists in one turn.")]
    [Min(100)]
    public int maxEvidenceChars = 900;

    [Tooltip("Conversation transcript budget before aggressive tail trim.")]
    [Min(120)]
    public int maxHistoryChars = 1200;

    [Tooltip("Player line cap (defensive against pasted essays).")]
    [Min(40)]
    public int maxPlayerUtteranceChars = 400;

    [Tooltip("Soft ceiling on assembled user prompt; history is shrunk first if exceeded.")]
    [Min(400)]
    public int maxTotalUserPromptChars = 3200;
}
