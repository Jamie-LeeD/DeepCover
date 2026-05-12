using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Global dialogue flow controller for scripted and runtime conversations.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [SerializeField] private DialogueUIView dialogueView;
    [SerializeField] private DialoguePlayerLock playerLock;
    [SerializeField] private UnityEvent onDialogueStarted;
    [SerializeField] private UnityEvent onDialogueEnded;

    private readonly List<DialogueLineData> activeLines = new List<DialogueLineData>();
    private string activeSpeakerName = string.Empty;
    private int currentLineIndex = -1;
    private bool isDialogueActive;
    private bool currentLineFinished;

    public event Action DialogueStarted;
    public event Action DialogueEnded;
    public event Action<int> LineChanged;

    public bool IsDialogueActive => isDialogueActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dialogueView == null)
        {
            dialogueView = FindFirstObjectByType<DialogueUIView>();
        }

        if (playerLock == null)
        {
            playerLock = FindFirstObjectByType<DialoguePlayerLock>();
        }

        if (dialogueView != null)
        {
            dialogueView.ContinueRequested += HandleContinueRequested;
        }

        if (dialogueView != null)
        {
            dialogueView.SetPanelVisible(false);
        }
    }

    private void OnDestroy()
    {
        if (dialogueView != null)
        {
            dialogueView.ContinueRequested -= HandleContinueRequested;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!isDialogueActive)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseDialogue();
        }
    }

    public void StartDialogue(DialogueSequence sequence)
    {
        if (sequence == null || sequence.Lines == null || sequence.Lines.Length == 0)
        {
            Debug.LogWarning($"{nameof(DialogueManager)} received an empty dialogue sequence.", this);
            return;
        }

        if (isDialogueActive)
        {
            CloseDialogue();
        }

        activeSpeakerName = sequence.SpeakerName;
        activeLines.Clear();
        activeLines.AddRange(sequence.Lines);
        currentLineIndex = -1;
        isDialogueActive = true;
        currentLineFinished = false;

        playerLock?.SetDialogueActive(true);
        dialogueView?.SetPanelVisible(true);
        onDialogueStarted?.Invoke();
        DialogueStarted?.Invoke();
        ShowNextLine();
    }

    public void StartDialogue(string speakerName, IEnumerable<DialogueLineData> lines)
    {
        if (lines == null)
        {
            return;
        }

        DialogueLineData[] lineArray = new List<DialogueLineData>(lines).ToArray();
        StartDialogue(new DialogueSequence(speakerName, lineArray));
    }

    public void CloseDialogue()
    {
        if (!isDialogueActive)
        {
            return;
        }

        if (currentLineIndex >= 0 && currentLineIndex < activeLines.Count)
        {
            activeLines[currentLineIndex].InvokeLineFinished();
        }

        isDialogueActive = false;
        currentLineIndex = -1;
        activeLines.Clear();
        currentLineFinished = false;

        dialogueView?.SetPanelVisible(false);
        playerLock?.SetDialogueActive(false);
        onDialogueEnded?.Invoke();
        DialogueEnded?.Invoke();
    }

    private void HandleContinueRequested()
    {
        if (!isDialogueActive)
        {
            return;
        }

        if (dialogueView != null && dialogueView.IsRevealing)
        {
            dialogueView.CompleteReveal();
            return;
        }

        if (!currentLineFinished)
        {
            return;
        }

        ShowNextLine();
    }

    private void ShowNextLine()
    {
        if (currentLineIndex >= 0 && currentLineIndex < activeLines.Count)
        {
            activeLines[currentLineIndex].InvokeLineFinished();
        }

        currentLineIndex++;

        if (currentLineIndex >= activeLines.Count)
        {
            CloseDialogue();
            return;
        }

        DialogueLineData line = activeLines[currentLineIndex];
        currentLineFinished = false;
        line.InvokeLineStarted();
        LineChanged?.Invoke(currentLineIndex);

        string speakerName = string.IsNullOrWhiteSpace(line.SpeakerNameOverride)
            ? activeSpeakerName
            : line.SpeakerNameOverride;

        dialogueView?.DisplayLine(speakerName, line.Text, HandleLineRevealCompleted);
    }

    private void HandleLineRevealCompleted()
    {
        currentLineFinished = true;
    }
}
