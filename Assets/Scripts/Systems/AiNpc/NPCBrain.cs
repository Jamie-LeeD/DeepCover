using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Drives contextual NPC lines via Ollama using <see cref="NPCProfile"/>, suspicion, evidence, trust, and memory.
/// <para />
/// <b>Gameplay loop (recommended wiring):</b>
/// <list type="number">
/// <item>Player uses <see cref="PlayerInteractor"/> (raycast + E) → this component's <see cref="IInteractable.Interact"/>.</item>
/// <item>Optional <see cref="DialogueQuestionInputView"/> collects a typed question while <see cref="DialoguePlayerLock"/> freezes movement.</item>
/// <item><see cref="NpcOllamaPromptBuilder"/> builds system/user prompts from profile + <see cref="SuspicionManager"/> + <see cref="EvidenceInventory"/> + <see cref="NPCMemory"/>.</item>
/// <item><see cref="OllamaClient"/> returns text → <see cref="NpcDialogueResponseFormatter"/> clamps length.</item>
/// <item><see cref="DialogueManager"/> shows the reply; player presses Continue / ESC to close.</item>
/// <item><see cref="OnConversationTurnComplete"/> and trust / suspicion deltas let you update world state without tight coupling.</item>
/// </list>
/// </summary>
public class NPCBrain : MonoBehaviour, IInteractable
{
    [Header("Profile")]
    [SerializeField] private NPCProfile profile;

    [Header("State")]
    [SerializeField] [Range(0f, 100f)] private float trustLevel = 40f;
    [SerializeField] private bool applyProfileStartingTrustOnAwake = true;

    [Header("Ollama")]
    [SerializeField] private string ollamaBaseUrl = "http://localhost:11434";
    [SerializeField] private string ollamaModel = "llama3";
    [SerializeField] private int ollamaTimeoutSeconds = 120;

    [Header("Conversation")]
    [SerializeField] private int maxMemoryTurns = 16;
    [SerializeField] private int maxMemorySummaryNotes = 8;
    [SerializeField] private string defaultPlayerUtteranceOnInteract = "Hello.";
    [SerializeField] private bool openDialogueWithReply = true;
    [Tooltip("When true, opens Dialogue UI immediately and streams Ollama tokens into the panel.")]
    [SerializeField] private bool useStreamingResponses = true;
    [Tooltip("Optional component implementing IDialogueBranchingDialogue for future choice UI.")]
    [SerializeField] private MonoBehaviour branchingDialogueSource;

    [Header("Player question (optional)")]
    [Tooltip("When set, Interact opens this UI first so the player can type a question before Ollama runs.")]
    [SerializeField] private DialogueQuestionInputView questionInputView;
    [SerializeField] private bool requireTypedQuestionBeforeAi;
    [Tooltip("Locks movement / cursor during the question UI and during DialogueManager lines.")]
    [SerializeField] private DialoguePlayerLock dialoguePlayerLock;

    [Header("AI prompts")]
    [SerializeField] private NpcPromptBudget promptBudget = new NpcPromptBudget();
    [Tooltip("Optional: component implementing IConversationHistoryTrimmer (e.g. custom summarizer). Uses tail trimmer when empty.")]
    [SerializeField] private UnityEngine.Object optionalHistoryTrimmer;
    [SerializeField] private bool logBuiltPromptToConsole;

    [Header("Post-turn tuning (optional)")]
    [Tooltip("Applied after a successful Ollama response (designer tuning; use events for complex logic).")]
    [SerializeField] private float trustDeltaOnSuccessfulReply;
    [Tooltip("Positive values call SuspicionManager.AddSuspicion; negative values call ReduceSuspicion.")]
    [SerializeField] private float suspicionDeltaOnSuccessfulReply;
    [Tooltip("When true, stores the NPC reply as a short summary note for future prompts (in addition to the dialogue line memory).")]
    [SerializeField] private bool recordNpcReplyAsSummaryNote;

    [Header("Events")]
    [SerializeField] private UnityEvent<string> onAiReplyReady;
    [SerializeField] private UnityEvent<string> onAiRequestFailed;
    [Tooltip("Invoked after a successful AI turn with (playerQuestion, npcReply). Wire to custom emotional state, quests, audio, etc.")]
    [SerializeField] private UnityEvent<string, string> onConversationTurnComplete;

    private OllamaClient ollamaClient;
    private NPCMemory memory;
    private NpcOllamaPromptBuilder promptBuilder;
    private bool requestInFlight;
    private bool waitingForManagedDialogueClose;
    private string pendingPlayerUtterance;
    private OllamaCancelToken activeStreamCancel;

    public NPCProfile Profile => profile;
    public float TrustLevel => trustLevel;

    public event Action<string, string> ConversationTurnComplete;

    /// <summary>
    /// Queue a player line for the next AI reply (e.g. from a future dialogue wheel).
    /// Cleared after one use if interact uses default instead.
    /// </summary>
    public void QueuePlayerUtterance(string utterance)
    {
        pendingPlayerUtterance = utterance;
    }

    /// <summary>
    /// Lets external UI (or the question view) push a line without using the interaction raycast.
    /// </summary>
    public void SubmitPlayerQuestion(string utterance)
    {
        if (requestInFlight || profile == null)
        {
            return;
        }

        StartCoroutine(RunFullConversationCoroutine(utterance.Trim()));
    }

    public void SetTrustLevel(float value)
    {
        trustLevel = Mathf.Clamp(value, 0f, 100f);
    }

    public void AdjustTrust(float delta)
    {
        SetTrustLevel(trustLevel + delta);
    }

    /// <summary>
    /// Designer hook for “important” facts that should survive in the prompt transcript block.
    /// </summary>
    public void AddMemorySummaryNote(string note)
    {
        memory?.AddSummaryNote(note);
    }

    public void ClearConversationMemory()
    {
        memory?.Clear();
    }

    private void Awake()
    {
        memory = new NPCMemory(maxMemoryTurns, maxMemorySummaryNotes);
        ollamaClient = new OllamaClient
        {
            BaseUrl = ollamaBaseUrl,
            Model = ollamaModel,
            TimeoutSeconds = ollamaTimeoutSeconds
        };

        if (promptBudget == null)
        {
            promptBudget = new NpcPromptBudget();
        }

        IConversationHistoryTrimmer trimmer = optionalHistoryTrimmer as IConversationHistoryTrimmer;
        promptBuilder = new NpcOllamaPromptBuilder(promptBudget, trimmer);

        if (applyProfileStartingTrustOnAwake && profile != null)
        {
            trustLevel = profile.StartingTrust;
        }

        if (dialoguePlayerLock == null)
        {
            dialoguePlayerLock = UnityEngine.Object.FindFirstObjectByType<DialoguePlayerLock>();
        }

        if (questionInputView == null)
        {
            questionInputView = UnityEngine.Object.FindFirstObjectByType<DialogueQuestionInputView>(
                FindObjectsInactive.Include);
        }
    }

    private void OnEnable()
    {
        SubscribeDialogueEnded();
    }

    private void Start()
    {
        SubscribeDialogueEnded();
    }

    private void SubscribeDialogueEnded()
    {
        if (DialogueManager.Instance == null)
        {
            return;
        }

        DialogueManager.Instance.DialogueEnded -= HandleAnyDialogueEnded;
        DialogueManager.Instance.DialogueEnded += HandleAnyDialogueEnded;
    }

    private void OnDisable()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.DialogueEnded -= HandleAnyDialogueEnded;
        }
    }

    private void OnDestroy()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.DialogueEnded -= HandleAnyDialogueEnded;
        }
    }

    private void HandleAnyDialogueEnded()
    {
        if (activeStreamCancel != null)
        {
            activeStreamCancel.Cancel();
            activeStreamCancel = null;
        }

        if (!waitingForManagedDialogueClose)
        {
            return;
        }

        waitingForManagedDialogueClose = false;
        requestInFlight = false;
    }

    public string GetInteractionPrompt()
    {
        return profile != null ? $"talk to {profile.CharacterDisplayName}" : "talk";
    }

    public bool CanInteract(GameObject interactor)
    {
        if (profile == null || requestInFlight)
        {
            return false;
        }

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            return false;
        }

        return true;
    }

    public void Interact(GameObject interactor)
    {
        if (requireTypedQuestionBeforeAi && questionInputView != null)
        {
            StartCoroutine(QuestionThenAiFlow());
            return;
        }

        string playerLine = ResolvePlayerUtterance();
        StartCoroutine(RunFullConversationCoroutine(playerLine));
    }

    /// <summary>
    /// Runs an AI reply for an arbitrary player line (e.g. scripted prompts or future branching UI).
    /// </summary>
    public IEnumerator GenerateReplyCoroutine(string playerUtterance)
    {
        yield return RunFullConversationCoroutine(playerUtterance);
    }

    private IEnumerator QuestionThenAiFlow()
    {
        if (profile == null)
        {
            yield break;
        }

        requestInFlight = true;
        dialoguePlayerLock?.SetDialogueActive(true);

        bool received = false;
        bool cancelled = false;
        string captured = null;

        questionInputView.BeginSession(
            profile.CharacterDisplayName,
            q =>
            {
                captured = q;
                received = true;
            },
            () => { cancelled = true; });

        while (!received && !cancelled)
        {
            yield return null;
        }

        questionInputView.EndSessionSilently();

        if (cancelled || string.IsNullOrWhiteSpace(captured))
        {
            dialoguePlayerLock?.SetDialogueActive(false);
            requestInFlight = false;
            yield break;
        }

        yield return RunAiTurnCoroutine(captured.Trim());

        if (!waitingForManagedDialogueClose)
        {
            requestInFlight = false;
        }
    }

    private IEnumerator RunFullConversationCoroutine(string playerUtterance)
    {
        requestInFlight = true;
        yield return RunAiTurnCoroutine(playerUtterance);
        if (!waitingForManagedDialogueClose)
        {
            requestInFlight = false;
        }
    }

    /// <summary>
    /// Core pipeline: memory line → prompt → Ollama → clean → optional DialogueManager.
    /// </summary>
    private IEnumerator RunAiTurnCoroutine(string playerUtterance)
    {
        if (profile == null)
        {
            yield break;
        }

        SyncOllamaSettings();
        memory.AddPlayerUtterance(playerUtterance);

        NpcDialoguePromptContext context = BuildPromptContext(playerUtterance);
        NpcOllamaPromptResult prompt = promptBuilder.Build(profile, context);

        if (logBuiltPromptToConsole)
        {
            Debug.Log(prompt.BuildCombinedDebugView(), this);
        }

        bool streamToDialogue = useStreamingResponses &&
                                openDialogueWithReply &&
                                DialogueManager.Instance != null;

        if (streamToDialogue)
        {
            yield return RunStreamingAiTurnCoroutine(prompt, playerUtterance);
            yield break;
        }

        OllamaResult result = default;
        IEnumerator request = ollamaClient.GenerateAsync(prompt.UserPrompt, prompt.SystemPrompt, r => { result = r; });
        while (request.MoveNext())
        {
            yield return request.Current;
        }

        string reply = BuildReplyFromResult(result);

        memory.AddNpcReply(reply);

        if (result.Success && recordNpcReplyAsSummaryNote && !string.IsNullOrWhiteSpace(reply))
        {
            memory.AddSummaryNote(reply);
        }

        if (result.Success)
        {
            ApplyPostTurnSystems(playerUtterance, reply);
        }

        if (openDialogueWithReply && DialogueManager.Instance != null)
        {
            waitingForManagedDialogueClose = true;
            DialogueLineData line = DialogueLineData.CreateRuntime(reply, profile.CharacterDisplayName);
            DialogueSequence sequence = new DialogueSequence(profile.CharacterDisplayName, new[] { line });
            DialogueManager.Instance.StartDialogue(sequence);
        }
    }

    private IEnumerator RunStreamingAiTurnCoroutine(NpcOllamaPromptResult prompt, string playerUtterance)
    {
        DialogueManager dialogue = DialogueManager.Instance;
        waitingForManagedDialogueClose = true;

        activeStreamCancel = new OllamaCancelToken();
        dialogue.BeginStreamingLine(profile.CharacterDisplayName, activeStreamCancel);

        OllamaResult result = default;
        IEnumerator stream = ollamaClient.GenerateStreamAsync(
            prompt.UserPrompt,
            prompt.SystemPrompt,
            delta => dialogue.AppendStreamingLineText(delta),
            r => { result = r; },
            activeStreamCancel);

        while (stream.MoveNext())
        {
            if (activeStreamCancel != null && activeStreamCancel.IsCancellationRequested)
            {
                break;
            }

            yield return stream.Current;
        }

        activeStreamCancel = null;

        if (result.ErrorMessage == "Cancelled." ||
            (dialogue != null && !dialogue.IsDialogueActive))
        {
            requestInFlight = false;
            waitingForManagedDialogueClose = false;
            yield break;
        }

        string reply = BuildReplyFromResult(result);

        memory.AddNpcReply(reply);

        if (result.Success && recordNpcReplyAsSummaryNote && !string.IsNullOrWhiteSpace(reply))
        {
            memory.AddSummaryNote(reply);
        }

        if (result.Success)
        {
            ApplyPostTurnSystems(playerUtterance, reply);
        }

        dialogue.CompleteStreamingLine(reply);
    }

    private string BuildReplyFromResult(OllamaResult result)
    {
        if (result.Success)
        {
            string text = NpcDialogueResponseFormatter.ClampToMaxSentences(result.Text);
            onAiReplyReady?.Invoke(text);
            return text;
        }

        onAiRequestFailed?.Invoke(result.ErrorMessage);
        Debug.LogWarning($"[NPCBrain] {profile.CharacterDisplayName}: {result.ErrorMessage}", this);
        return string.IsNullOrWhiteSpace(profile.FallbackLineOnAiFailure)
            ? "…"
            : profile.FallbackLineOnAiFailure;
    }

    private void ApplyPostTurnSystems(string playerUtterance, string npcReply)
    {
        if (Mathf.Abs(trustDeltaOnSuccessfulReply) > 0.0001f)
        {
            AdjustTrust(trustDeltaOnSuccessfulReply);
        }

        if (Mathf.Abs(suspicionDeltaOnSuccessfulReply) > 0.0001f && SuspicionManager.Instance != null)
        {
            if (suspicionDeltaOnSuccessfulReply > 0f)
            {
                SuspicionManager.Instance.AddSuspicion(suspicionDeltaOnSuccessfulReply);
            }
            else
            {
                SuspicionManager.Instance.ReduceSuspicion(-suspicionDeltaOnSuccessfulReply);
            }
        }

        onConversationTurnComplete?.Invoke(playerUtterance, npcReply);
        ConversationTurnComplete?.Invoke(playerUtterance, npcReply);
    }

    private string ResolvePlayerUtterance()
    {
        if (branchingDialogueSource is IDialogueBranchingDialogue branching &&
            branching.TryConsumePlayerChoice(out string choice) &&
            !string.IsNullOrWhiteSpace(choice))
        {
            return choice.Trim();
        }

        if (!string.IsNullOrWhiteSpace(pendingPlayerUtterance))
        {
            string pending = pendingPlayerUtterance.Trim();
            pendingPlayerUtterance = null;
            return pending;
        }

        return defaultPlayerUtteranceOnInteract;
    }

    private NpcDialoguePromptContext BuildPromptContext(string playerUtterance)
    {
        float suspicionValue = 0f;
        SuspicionLevel suspicionLevel = SuspicionLevel.Clear;
        string suspicionTag = string.Empty;
        if (SuspicionManager.Instance != null)
        {
            suspicionValue = SuspicionManager.Instance.SuspicionValue;
            suspicionLevel = SuspicionManager.Instance.CurrentLevel;
            if (SuspicionManager.Instance is ISuspicionDialogueContext dialogueContext)
            {
                suspicionTag = dialogueContext.GetSuspicionDialogueTag();
            }
        }

        string evidenceSummary = "No evidence inventory in scene.";
        if (EvidenceInventory.Instance is IEvidenceContextProvider evidenceContext)
        {
            evidenceSummary = evidenceContext.BuildEvidenceContextSummary();
        }
        else if (EvidenceInventory.Instance != null)
        {
            evidenceSummary = EvidenceInventory.Instance.BuildEvidenceContextSummary();
        }

        return new NpcDialoguePromptContext(
            profile.CharacterDisplayName,
            trustLevel,
            suspicionValue,
            suspicionLevel,
            suspicionTag,
            evidenceSummary,
            memory.BuildTranscript(),
            playerUtterance);
    }

    private void SyncOllamaSettings()
    {
        ollamaClient.BaseUrl = ollamaBaseUrl;
        ollamaClient.Model = ollamaModel;
        ollamaClient.TimeoutSeconds = ollamaTimeoutSeconds;
    }
}
