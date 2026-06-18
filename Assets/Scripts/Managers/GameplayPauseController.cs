using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>
/// Toggles pause with Escape, freezes time, and coordinates with dialogue / question UI.
/// </summary>
/// <remarks>
/// Attach to a Managers object (e.g. alongside DialogueManager). Assign <see cref="PauseMenuView"/> and player refs.
/// Escape priority: Resume pause → Close dialogue → Cancel question input → Open pause.
/// </remarks>
[DisallowMultipleComponent]
public class GameplayPauseController : MonoBehaviour
{
    public static GameplayPauseController Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private PauseMenuView pauseMenuView;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Player (optional — auto-found when empty)")]
    [SerializeField] private FirstPersonController firstPersonController;
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private Rigidbody playerRigidbody;

    [Header("Options")]
    [SerializeField] private bool lockCursorOnResume = true;
    [SerializeField] private string mainMenuSceneName = "StartScene";

    private bool isPaused;
    private InputAction pauseAction;
    private InputAction uiCancelAction;
    private bool escapeHeldLastFrame;
    private bool pauseRequestedFromInputEvent;

    public bool IsPaused => isPaused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (SceneManager.GetActiveScene().name == "StartScene")
        {
            return;
        }

        EnsureExists();
    }

    public static GameplayPauseController EnsureExists()
    {
        GameplayPauseController existing =
            FindFirstObjectByType<GameplayPauseController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            existing.enabled = true;
            return existing;
        }

        GameObject managers = GameObject.Find("Managers") ?? new GameObject("Managers");
        GameplayPauseController controller = managers.AddComponent<GameplayPauseController>();
        return controller;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsurePauseAction();
        ResolveInputActions();

        if (pauseMenuView == null)
        {
            pauseMenuView = GetComponentInChildren<PauseMenuView>(true);
        }

        if (pauseMenuView == null)
        {
            pauseMenuView = PauseMenuRuntimeBuilder.CreateOrGet();
        }

        ResolvePlayerReferences();
        pauseMenuView?.SetVisible(false);
        EnsureTimeRunning();
    }

    private void OnEnable()
    {
        EnsurePauseAction();
        ResolveInputActions();
        pauseAction?.Enable();
        uiCancelAction?.Enable();
        InputSystem.onEvent -= HandleInputSystemEvent;
        InputSystem.onEvent += HandleInputSystemEvent;
    }

    private void OnDisable()
    {
        InputSystem.onEvent -= HandleInputSystemEvent;
        pauseAction?.Disable();
        uiCancelAction?.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (isPaused)
        {
            Time.timeScale = 1f;
        }
    }

    private void Update()
    {
        if (pauseRequestedFromInputEvent)
        {
            pauseRequestedFromInputEvent = false;
            escapeHeldLastFrame = Keyboard.current != null && Keyboard.current.escapeKey.isPressed;
            HandleEscapePressed();
            return;
        }

        if (!WasPausePressedThisFrame())
        {
            return;
        }

        HandleEscapePressed();
    }

    private void HandleEscapePressed()
    {
        if (IntroNarrativeController.IsIntroActive)
        {
            Debug.Log("[GameplayPause] Pause blocked: intro narrative is active.", this);
            return;
        }

        if (GameStateManager.Instance != null && GameStateManager.Instance.IsTerminalState)
        {
            Debug.Log("[GameplayPause] Pause blocked: terminal win/lose screen is active.", this);
            return;
        }

        EvidenceInventoryUI evidenceJournal = FindFirstObjectByType<EvidenceInventoryUI>(FindObjectsInactive.Include);
        if (evidenceJournal != null && evidenceJournal.IsOpen)
        {
            Debug.Log("[GameplayPause] Pause blocked: closing Evidence Journal first.", this);
            evidenceJournal.CloseJournal();
            return;
        }

        if (isPaused)
        {
            ResumeGame();
            return;
        }

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            Debug.Log("[GameplayPause] Pause blocked: closing Dialogue first.", this);
            DialogueManager.Instance.CloseDialogue();
            return;
        }

        DialogueQuestionInputView questionView = FindFirstObjectByType<DialogueQuestionInputView>(FindObjectsInactive.Include);
        if (questionView != null && questionView.IsSessionActive)
        {
            Debug.Log("[GameplayPause] Pause blocked: cancelling Dialogue Question input first.", this);
            questionView.CancelSession();
            return;
        }

        PauseGame();
    }

    public void PauseGame()
    {
        if (isPaused)
        {
            return;
        }

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            return;
        }

        isPaused = true;
        Time.timeScale = 0f;

        SetPlayerControlEnabled(false);
        StopPlayerMotion();
        UnlockCursor();
        pauseMenuView?.SetVisible(true);
        Debug.Log("Game Paused", this);
    }

    public void ResumeGame()
    {
        if (!isPaused)
        {
            return;
        }

        isPaused = false;
        Time.timeScale = 1f;

        pauseMenuView?.SetVisible(false);
        SetPlayerControlEnabled(true);

        bool dialogueOrQuestionUi =
            (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive) ||
            IsQuestionSessionActive();

        if (lockCursorOnResume && !dialogueOrQuestionUi)
        {
            LockCursor();
        }
        else if (dialogueOrQuestionUi)
        {
            UnlockCursor();
        }

        Debug.Log("Game Resumed", this);
    }

    public void RestartCurrentScene()
    {
        EnsureTimeRunning();
        isPaused = false;
        pauseMenuView?.SetVisible(false);

        Scene active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.buildIndex);
    }

    public void LoadMainMenu()
    {
        Debug.Log("Loading Main Menu", this);

        EnsureTimeRunning();
        isPaused = false;
        pauseMenuView?.SetVisible(false);
        SetPlayerControlEnabled(false);
        UnlockCursor();

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        Debug.LogWarning(
            $"[GameplayPause] Main menu scene '{mainMenuSceneName}' is not in Build Settings. Reloading current scene instead.",
            this);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ExitGame()
    {
        EnsureTimeRunning();

#if UNITY_EDITOR
        Debug.Log("[GameplayPause] Exit Game requested — stopping Play Mode in Editor.");
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Debug.Log("[GameplayPause] Exit Game — Application.Quit()");
        Application.Quit();
#endif
    }

    private void ResolvePlayerReferences()
    {
        if (firstPersonController == null)
        {
            firstPersonController = FindFirstObjectByType<FirstPersonController>();
        }

        if (playerInteractor == null)
        {
            playerInteractor = FindFirstObjectByType<PlayerInteractor>();
        }

        if (playerRigidbody == null && firstPersonController != null)
        {
            playerRigidbody = firstPersonController.GetComponent<Rigidbody>();
        }
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

    private static bool IsQuestionSessionActive()
    {
        DialogueQuestionInputView questionView =
            FindFirstObjectByType<DialogueQuestionInputView>(FindObjectsInactive.Include);
        return questionView != null && questionView.IsSessionActive;
    }

    private void EnsurePauseAction()
    {
        if (pauseAction != null)
        {
            return;
        }

        pauseAction = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
    }

    private void ResolveInputActions()
    {
        if (uiCancelAction != null)
        {
            return;
        }

        if (inputActions == null)
        {
            inputActions = LoadDefaultInputActions();
        }

        if (inputActions == null)
        {
            Debug.LogWarning("[GameplayPause] No InputSystem_Actions asset found; using direct keyboard fallback only.", this);
            return;
        }

        InputActionMap uiMap = inputActions.FindActionMap("UI", false);
        uiCancelAction = uiMap?.FindAction("Cancel", false);
        if (uiCancelAction == null)
        {
            Debug.LogWarning("[GameplayPause] UI/Cancel action not found; using direct keyboard fallback only.", this);
        }
        else
        {
        }
    }

    private bool WasPausePressedThisFrame()
    {
        bool actionPressed = pauseAction != null && pauseAction.WasPressedThisFrame();
        bool cancelPressed = uiCancelAction != null && uiCancelAction.WasPressedThisFrame();

        bool keyboardPressed = false;
        if (Keyboard.current != null)
        {
            bool escapeDown = Keyboard.current.escapeKey.isPressed;
            keyboardPressed = escapeDown && !escapeHeldLastFrame;
            escapeHeldLastFrame = escapeDown;
        }
        else
        {
            escapeHeldLastFrame = false;
        }

        return cancelPressed || actionPressed || keyboardPressed;
    }

    private static InputActionAsset LoadDefaultInputActions()
    {
        InputActionAsset[] assets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
        for (int i = 0; i < assets.Length; i++)
        {
            InputActionAsset asset = assets[i];
            if (asset != null && asset.name == "InputSystem_Actions")
            {
                return asset;
            }
        }

        return null;
    }

    private void HandleInputSystemEvent(InputEventPtr eventPtr, InputDevice device)
    {
        if (device is not Keyboard keyboard || !eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
        {
            return;
        }

        if (keyboard.escapeKey.ReadValueFromEvent(eventPtr) > 0f)
        {
            pauseRequestedFromInputEvent = true;
        }
    }

    private static void EnsureTimeRunning()
    {
        Time.timeScale = 1f;
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
