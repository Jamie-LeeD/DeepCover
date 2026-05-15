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
    private bool isStreamingLine;
    private OllamaCancelToken activeStreamCancel;

    public event Action DialogueStarted;
    public event Action DialogueEnded;
    public event Action<int> LineChanged;

    public bool IsDialogueActive => isDialogueActive;
    public bool IsStreamingLine => isStreamingLine;

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

        CancelActiveStream();

        if (currentLineIndex >= 0 && currentLineIndex < activeLines.Count)
        {
            activeLines[currentLineIndex].InvokeLineFinished();
        }

        isDialogueActive = false;
        isStreamingLine = false;
        currentLineIndex = -1;
        activeLines.Clear();
        currentLineFinished = false;

        dialogueView?.CancelStreamingLine();
        dialogueView?.SetEvidenceOverlay(null);
        dialogueView?.SetPanelVisible(false);
        playerLock?.SetDialogueActive(false);
        onDialogueEnded?.Invoke();
        DialogueEnded?.Invoke();
    }

    /// <summary>
    /// Opens dialogue UI immediately and streams NPC text as Ollama tokens arrive.
    /// </summary>
    public void BeginStreamingLine(string speakerName, OllamaCancelToken cancelToken = null)
    {
        if (isDialogueActive)
        {
            CloseDialogue();
        }

        activeStreamCancel = cancelToken;
        activeSpeakerName = speakerName ?? string.Empty;
        activeLines.Clear();
        currentLineIndex = 0;
        isDialogueActive = true;
        isStreamingLine = true;
        currentLineFinished = false;

        playerLock?.SetDialogueActive(true);
        dialogueView?.SetPanelVisible(true);
        dialogueView?.BeginStreamingLine(activeSpeakerName);
        onDialogueStarted?.Invoke();
        DialogueStarted?.Invoke();
    }

    /// <summary>Appends a streamed token to the active line.</summary>
    public void AppendStreamingLineText(string chunk)
    {
        if (!isStreamingLine)
        {
            return;
        }

        dialogueView?.AppendStreamingText(chunk);
    }

    /// <summary>Finalizes streamed text and enables Continue.</summary>
    public void CompleteStreamingLine(string finalText)
    {
        if (!isStreamingLine)
        {
            return;
        }

        isStreamingLine = false;
        activeStreamCancel = null;

        dialogueView?.EndStreamingLine(finalText, HandleLineRevealCompleted);
    }

    /// <summary>Aborts the in-flight Ollama stream (ESC / close dialogue).</summary>
    public void CancelActiveStream()
    {
        if (activeStreamCancel == null)
        {
            return;
        }

        activeStreamCancel.Cancel();
        activeStreamCancel = null;
        isStreamingLine = false;
    }

    private void HandleContinueRequested()
    {
        if (!isDialogueActive)
        {
            return;
        }

        if (isStreamingLine)
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

        dialogueView?.DisplayLine(speakerName, line.Text, HandleLineRevealCompleted, line.EvidenceShownWithLine);
    }

    private void HandleLineRevealCompleted()
    {
        currentLineFinished = true;
    }
}
