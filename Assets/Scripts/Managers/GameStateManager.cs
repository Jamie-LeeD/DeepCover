using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum DeepCoverGameState
{
    Playing = 0,
    Won = 1,
    Lost = 2
}

/// <summary>
/// Central win/lose controller for Deep Cover.
/// Suspicion loss, accusation outcomes, required evidence checks, player lock, UI, and scene transitions live here.
/// </summary>
[DisallowMultipleComponent]
public class GameStateManager : MonoBehaviour
{
    private const string VictoryMessage =
        "You have discovered the true culprit behind the spy leaks.\n\n" +
        "ARCHIVE manipulated Helix Dynamics and exposed intelligence agents for its own objectives.";

    public static GameStateManager Instance { get; private set; }

    [Header("Culprit")]
    [SerializeField] private string trueCulpritId = "ARCHIVE";
    [SerializeField] private string[] trueCulpritAliases = { "archive", "the ai", "ai system" };

    [Header("Evidence Requirements")]
    [Tooltip("Explicit required evidence. If empty and Use All Resources Evidence When Empty is true, all EvidenceData assets in Resources/Evidence are required.")]
    [SerializeField] private EvidenceData[] requiredEvidence;
    [SerializeField] private string[] requiredEvidenceIds;
    [SerializeField] private bool useAllResourcesEvidenceWhenEmpty = true;

    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "StartScene";

    [Header("UI")]
    [SerializeField] private GameEndScreenView endScreenView;

    [Header("Player Lock")]
    [SerializeField] private FirstPersonController firstPersonController;
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private Rigidbody playerRigidbody;

    [Header("Debug")]
    [SerializeField] private bool logGameStateDebug;

    public DeepCoverGameState CurrentState { get; private set; } = DeepCoverGameState.Playing;
    public bool IsTerminalState => CurrentState == DeepCoverGameState.Won || CurrentState == DeepCoverGameState.Lost;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
        endScreenView?.Hide();
        Time.timeScale = 1f;
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
        EnsureRequiredEvidenceIds();
        EvaluateMaximumSuspicionLoss("Start");
    }

    private void OnDisable()
    {
        if (SuspicionManager.Instance != null)
        {
            SuspicionManager.Instance.SuspicionChanged -= HandleSuspicionChanged;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        // Fallback safety check: the event subscription should catch this immediately,
        // but polling while playing prevents missed subscriptions or runtime-created managers
        // from leaving the game in an invalid state.
        EvaluateMaximumSuspicionLoss("Update");
    }

    public void AccuseArchive()
    {
        Accuse(trueCulpritId);
    }

    public void Accuse(NPCProfile profile)
    {
        if (profile == null)
        {
            TriggerMissionFailed("You accused the wrong suspect.");
            return;
        }

        Accuse(profile.SuspectID, profile.AccusationDisplayName);
    }

    public void Accuse(string accusedIdOrName)
    {
        Accuse(accusedIdOrName, accusedIdOrName);
    }

    public void Accuse(string accusedIdOrName, string accusedDisplayName)
    {
        if (IsTerminalState)
        {
            return;
        }

        string accused = string.IsNullOrWhiteSpace(accusedIdOrName) ? "Unknown Suspect" : accusedIdOrName.Trim();
        if (!IsTrueCulprit(accused))
        {
            TriggerMissionFailed("You accused the wrong suspect.");
            return;
        }

        if (!HasAllRequiredEvidence(out string missingSummary))
        {
            TriggerGameOver(
                "ARCHIVE was accused before the case was complete.\n" +
                $"Missing evidence: {missingSummary}");
            return;
        }

        TriggerVictory();
    }

    public bool IsInvestigationComplete()
    {
        GetInvestigationProgress(out _, out _, out _, out bool complete);
        return complete;
    }

    public void GetInvestigationProgress(
        out int collectedEvidence,
        out int requiredEvidenceCount,
        out string missingSummary,
        out bool complete)
    {
        EnsureRequiredEvidenceIds();

        collectedEvidence = EvidenceInventory.Instance != null
            ? EvidenceInventory.Instance.CollectedRecords.Count
            : 0;
        requiredEvidenceCount = requiredEvidenceIds != null ? requiredEvidenceIds.Length : 0;

        complete = HasAllRequiredEvidence(out missingSummary);
    }

    /// <summary>
    /// Parses player text for explicit accusation intent. Used by NPCBrain so typed dialogue can resolve the case.
    /// Returns true when the text was an accusation and the game state consumed it.
    /// </summary>
    public bool TryHandleAccusationText(string playerText, string currentNpcName)
    {
        if (IsTerminalState || string.IsNullOrWhiteSpace(playerText) || !ContainsAccusationIntent(playerText))
        {
            return false;
        }

        string normalized = Normalize(playerText);
        if (ContainsTrueCulpritReference(normalized))
        {
            Accuse(trueCulpritId);
            return true;
        }

        if (normalized.Contains("you") || normalized.Contains("yourself"))
        {
            Accuse(currentNpcName);
            return true;
        }

        Accuse(ExtractNamedSuspect(playerText, currentNpcName));
        return true;
    }

    public bool HasAllRequiredEvidence(out string missingSummary)
    {
        EnsureRequiredEvidenceIds();

        if (requiredEvidenceIds == null || requiredEvidenceIds.Length == 0)
        {
            missingSummary = "none";
            return true;
        }

        if (EvidenceInventory.Instance == null)
        {
            missingSummary = "evidence inventory unavailable";
            return false;
        }

        List<string> missing = new List<string>();
        for (int i = 0; i < requiredEvidenceIds.Length; i++)
        {
            string id = requiredEvidenceIds[i];
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!EvidenceInventory.Instance.HasEvidenceById(id))
            {
                missing.Add(id);
            }
        }

        missingSummary = missing.Count == 0 ? "none" : string.Join(", ", missing);
        return missing.Count == 0;
    }

    public void TriggerGameOver(string reason)
    {
        if (IsTerminalState)
        {
            return;
        }

        if (logGameStateDebug)
        {
            Debug.Log($"[GameState] TriggerGameOver called. Reason: {reason}", this);
        }

        CurrentState = DeepCoverGameState.Lost;
        EnterTerminalState();
        EnsureEndScreenView();
        endScreenView?.ShowGameOver(reason);
    }

    public void TriggerMissionFailed(string reason)
    {
        if (IsTerminalState)
        {
            return;
        }

        if (logGameStateDebug)
        {
            Debug.Log($"[GameState] TriggerMissionFailed called. Reason: {reason}", this);
        }

        CurrentState = DeepCoverGameState.Lost;
        EnterTerminalState();
        EnsureEndScreenView();
        endScreenView?.ShowMissionFailed(
            string.IsNullOrWhiteSpace(reason) ? "You accused the wrong suspect." : reason);
    }

    public void TriggerVictory()
    {
        if (IsTerminalState)
        {
            return;
        }

        CurrentState = DeepCoverGameState.Won;
        EnterTerminalState();
        EnsureEndScreenView();
        if (endScreenView == null)
        {
            Debug.LogError("[GameState] Cannot display victory screen because no GameEndScreenView could be found or created.", this);
            return;
        }

        endScreenView.ShowVictory(VictoryMessage);
    }

    public void RetryCurrentScene()
    {
        Time.timeScale = 1f;
        CurrentState = DeepCoverGameState.Playing;
        EvidenceInventory.Instance?.ClearRuntimeEvidence();
        Scene active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.buildIndex);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        CurrentState = DeepCoverGameState.Playing;
        EvidenceInventory.Instance?.ClearRuntimeEvidence();

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        Debug.LogWarning(
            $"[GameState] Main menu scene '{mainMenuSceneName}' is not in Build Settings. Reloading current scene instead.",
            this);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        Debug.Log("[GameState] Quit Game requested in Unity Editor.");
#else
        Application.Quit();
#endif
    }

    private void HandleSuspicionChanged(float value)
    {
        EvaluateMaximumSuspicionLoss($"SuspicionChanged({value:0.###})");
    }

    private void EnterTerminalState()
    {
        Time.timeScale = 0f;

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            DialogueManager.Instance.CloseDialogue();
        }

        DialogueQuestionInputView questionInputView =
            FindFirstObjectByType<DialogueQuestionInputView>(FindObjectsInactive.Include);
        if (questionInputView != null && questionInputView.IsSessionActive)
        {
            questionInputView.Hide();
        }

        SetPlayerControlEnabled(false);
        StopPlayerMotion();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ResolveReferences()
    {
        if (endScreenView == null)
        {
            endScreenView = FindFirstObjectByType<GameEndScreenView>(FindObjectsInactive.Include);
        }

        if (endScreenView == null)
        {
            endScreenView = GameEndScreenRuntimeBuilder.CreateOrGet(this);
        }

        if (firstPersonController == null)
        {
            firstPersonController = FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
        }

        if (playerInteractor == null)
        {
            playerInteractor = FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        }

        if (playerRigidbody == null && firstPersonController != null)
        {
            playerRigidbody = firstPersonController.GetComponent<Rigidbody>();
        }
    }

    private void TrySubscribe()
    {
        if (SuspicionManager.Instance != null)
        {
            SuspicionManager.Instance.SuspicionChanged -= HandleSuspicionChanged;
            SuspicionManager.Instance.SuspicionChanged += HandleSuspicionChanged;
            EvaluateMaximumSuspicionLoss("TrySubscribe");
        }
        else if (logGameStateDebug)
        {
            Debug.Log("[GameState] TrySubscribe: SuspicionManager.Instance is null.", this);
        }
    }

    private void EvaluateMaximumSuspicionLoss(string source)
    {
        if (IsTerminalState)
        {
            return;
        }

        SuspicionManager suspicion = SuspicionManager.Instance;
        if (suspicion == null)
        {
            return;
        }

        float currentValue = suspicion.SuspicionValue;
        float threshold = SuspicionManager.MinReputation;
        bool shouldLose = currentValue <= threshold + 0.001f || suspicion.IsAtMaximumSuspicion;

        if (logGameStateDebug && (shouldLose || source != "Update"))
        {
            Debug.Log(
                $"[GameState] Max suspicion check ({source}): value={currentValue:0.###}, " +
                $"threshold={threshold:0.###}, level={suspicion.CurrentLevel}, shouldLose={shouldLose}",
                this);
        }

        if (shouldLose)
        {
            TriggerGameOver("Maximum suspicion reached. Your cover has been blown.");
        }
    }

    private void EnsureEndScreenView()
    {
        if (endScreenView == null)
        {
            endScreenView = FindFirstObjectByType<GameEndScreenView>(FindObjectsInactive.Include);
        }

        if (endScreenView == null)
        {
            endScreenView = GameEndScreenRuntimeBuilder.CreateOrGet(this);
        }

        if (logGameStateDebug)
        {
            Debug.Log($"[GameState] End screen reference resolved: {endScreenView != null}", this);
        }
    }

    private void EnsureRequiredEvidenceIds()
    {
        List<string> ids = new List<string>();
        AddEvidenceIds(ids, requiredEvidence);

        if (requiredEvidenceIds != null)
        {
            for (int i = 0; i < requiredEvidenceIds.Length; i++)
            {
                AddId(ids, requiredEvidenceIds[i]);
            }
        }

        if (ids.Count == 0 && useAllResourcesEvidenceWhenEmpty)
        {
            AddEvidenceIds(ids, Resources.LoadAll<EvidenceData>("Evidence"));
        }

        requiredEvidenceIds = ids.ToArray();
    }

    private static void AddEvidenceIds(List<string> ids, EvidenceData[] evidenceItems)
    {
        if (evidenceItems == null)
        {
            return;
        }

        for (int i = 0; i < evidenceItems.Length; i++)
        {
            if (evidenceItems[i] != null)
            {
                AddId(ids, evidenceItems[i].EvidenceId);
            }
        }
    }

    private static void AddId(List<string> ids, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || ids.Contains(id))
        {
            return;
        }

        ids.Add(id);
    }

    private bool IsTrueCulprit(string suspect)
    {
        string normalized = Normalize(suspect);
        if (normalized == Normalize(trueCulpritId))
        {
            return true;
        }

        return ContainsTrueCulpritReference(normalized);
    }

    private bool ContainsTrueCulpritReference(string normalizedText)
    {
        if (normalizedText.Contains(Normalize(trueCulpritId)))
        {
            return true;
        }

        if (trueCulpritAliases == null)
        {
            return false;
        }

        for (int i = 0; i < trueCulpritAliases.Length; i++)
        {
            string alias = Normalize(trueCulpritAliases[i]);
            if (!string.IsNullOrWhiteSpace(alias) && normalizedText.Contains(alias))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAccusationIntent(string text)
    {
        string normalized = Normalize(text);
        return normalized.Contains("accuse") ||
               normalized.Contains("culprit") ||
               normalized.Contains("responsible for the leaks") ||
               normalized.Contains("responsible for leaking") ||
               normalized.Contains("behind the leaks") ||
               normalized.Contains("leaker");
    }

    private static string ExtractNamedSuspect(string playerText, string fallback)
    {
        string normalized = Normalize(playerText);
        if (normalized.Contains("evelyn"))
        {
            return "Evelyn Cross";
        }

        if (normalized.Contains("marcus"))
        {
            return "Marcus";
        }

        if (normalized.Contains("mira"))
        {
            return "Mira";
        }

        return string.IsNullOrWhiteSpace(fallback) ? "Unknown Suspect" : fallback;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private void SetPlayerControlEnabled(bool enabled)
    {
        if (firstPersonController != null)
        {
            firstPersonController.enabled = enabled;
        }

        if (playerInteractor != null)
        {
            playerInteractor.enabled = enabled;
        }
    }

    private void StopPlayerMotion()
    {
        if (playerRigidbody == null)
        {
            return;
        }

        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;
    }
}
