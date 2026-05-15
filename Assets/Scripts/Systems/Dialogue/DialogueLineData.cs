using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// One line of dialogue with optional per-line events.
/// </summary>
[Serializable]
public class DialogueLineData
{
    [SerializeField] private string speakerNameOverride;
    [TextArea(2, 6)]
    [SerializeField] private string text;
    [SerializeField] private UnityEvent onLineStarted;
    [SerializeField] private UnityEvent onLineFinished;

    [Header("Evidence (optional)")]
    [Tooltip("When set, the dialogue UI can show this evidence card while the line is visible.")]
    [SerializeField] private EvidenceData evidenceShownWithLine;

    public string SpeakerNameOverride => speakerNameOverride;
    public string Text => text;
    public EvidenceData EvidenceShownWithLine => evidenceShownWithLine;

    public static DialogueLineData CreateRuntime(string lineText, string speakerOverride = "")
    {
        return new DialogueLineData
        {
            speakerNameOverride = speakerOverride ?? string.Empty,
            text = lineText ?? string.Empty
        };
    }

    public void InvokeLineStarted()
    {
        onLineStarted?.Invoke();
    }

    public void InvokeLineFinished()
    {
        onLineFinished?.Invoke();
    }
}

/// <summary>
/// Runtime dialogue payload for authored, dynamic, or generated conversations.
/// </summary>
[Serializable]
public class DialogueSequence
{
    [SerializeField] private string speakerName = "NPC";
    [SerializeField] private DialogueLineData[] lines = Array.Empty<DialogueLineData>();

    public string SpeakerName => speakerName;
    public DialogueLineData[] Lines => lines;

    public DialogueSequence(string speakerName, DialogueLineData[] lines)
    {
        this.speakerName = speakerName;
        this.lines = lines ?? Array.Empty<DialogueLineData>();
    }

    public static DialogueSequence FromRuntime(string speakerName, params string[] lines)
    {
        DialogueLineData[] lineData = new DialogueLineData[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            lineData[i] = DialogueLineData.CreateRuntime(lines[i]);
        }

        return new DialogueSequence(speakerName, lineData);
    }
}
