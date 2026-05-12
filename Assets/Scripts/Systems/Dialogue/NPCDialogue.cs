using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// NPC dialogue source that supports authored assets, inspector lines, and runtime generation.
/// </summary>
public class NPCDialogue : MonoBehaviour, IInteractable, IDialogueContentProvider
{
    [SerializeField] private string npcName = "NPC";
    [SerializeField] private DialogueAsset authoredDialogue;
    [SerializeField] private DialogueLineData[] manualLines;
    [SerializeField] private bool useRuntimeBuilder;
    [SerializeField] private MonoBehaviour runtimeDialogueBuilder;
    [SerializeField] private UnityEvent onDialogueStarted;
    [SerializeField] private UnityEvent onDialogueEnded;

    private DialogueLineData[] runtimeLines;
    private bool startedCurrentDialogue;

    public string SpeakerName => npcName;

    private void OnEnable()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.DialogueStarted += HandleDialogueStarted;
            DialogueManager.Instance.DialogueEnded += HandleDialogueEnded;
        }
    }

    private void OnDisable()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.DialogueStarted -= HandleDialogueStarted;
            DialogueManager.Instance.DialogueEnded -= HandleDialogueEnded;
        }
    }

    public string GetInteractionPrompt()
    {
        return $"talk to {npcName}";
    }

    public bool CanInteract(GameObject interactor)
    {
        return TryBuildSequence(out _);
    }

    public void Interact(GameObject interactor)
    {
        StartDialogue();
    }

    public void StartDialogue()
    {
        if (!TryBuildSequence(out DialogueSequence sequence))
        {
            Debug.LogWarning($"{nameof(NPCDialogue)} on {name} has no dialogue to play.", this);
            return;
        }

        if (DialogueManager.Instance == null)
        {
            Debug.LogError($"{nameof(DialogueManager)} is missing from the scene.", this);
            return;
        }

        startedCurrentDialogue = true;
        DialogueManager.Instance.StartDialogue(sequence);
    }

    public void SetRuntimeLines(IEnumerable<DialogueLineData> lines)
    {
        runtimeLines = lines == null ? null : new List<DialogueLineData>(lines).ToArray();
    }

    public void SetRuntimeLines(params string[] lines)
    {
        if (lines == null || lines.Length == 0)
        {
            runtimeLines = null;
            return;
        }

        DialogueLineData[] builtLines = new DialogueLineData[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            builtLines[i] = DialogueLineData.CreateRuntime(lines[i]);
        }

        runtimeLines = builtLines;
    }

    public bool TryGetDialogue(out DialogueSequence sequence)
    {
        return TryBuildSequence(out sequence);
    }

    private bool TryBuildSequence(out DialogueSequence sequence)
    {
        if (runtimeLines != null && runtimeLines.Length > 0)
        {
            sequence = new DialogueSequence(npcName, runtimeLines);
            return true;
        }

        if (useRuntimeBuilder && runtimeDialogueBuilder is IRuntimeDialogueBuilder builder)
        {
            List<DialogueLineData> builtLines = new List<DialogueLineData>();
            builder.BuildDialogue(builtLines);

            if (builtLines.Count > 0)
            {
                sequence = new DialogueSequence(npcName, builtLines.ToArray());
                return true;
            }
        }

        if (authoredDialogue != null && authoredDialogue.Lines != null && authoredDialogue.Lines.Length > 0)
        {
            sequence = new DialogueSequence(authoredDialogue.SpeakerName, authoredDialogue.Lines);
            return true;
        }

        if (manualLines != null && manualLines.Length > 0)
        {
            sequence = new DialogueSequence(npcName, manualLines);
            return true;
        }

        sequence = null;
        return false;
    }

    private void HandleDialogueStarted()
    {
        if (!startedCurrentDialogue)
        {
            return;
        }

        onDialogueStarted?.Invoke();
    }

    private void HandleDialogueEnded()
    {
        if (!startedCurrentDialogue)
        {
            return;
        }

        startedCurrentDialogue = false;
        onDialogueEnded?.Invoke();
    }
}
