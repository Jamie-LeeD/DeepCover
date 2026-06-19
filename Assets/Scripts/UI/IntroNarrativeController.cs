using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shows the Deep Cover briefing before gameplay starts, locks player input, and reveals text with a typewriter effect.
/// </summary>
[DisallowMultipleComponent]
public class IntroNarrativeController : MonoBehaviour
{
    private static readonly Color LightButtonTextColor = new Color(0f, 0.2f, 0.4f, 1f);

    private const string DefaultIntroText =
        "AGENT BRIEFING\n\n" +
        "You are an undercover operative assigned to investigate a series of intelligence leaks that have compromised active spy networks.\n\n" +
        "Evidence suggests the source of the leaks originates from Helix Dynamics, a cybersecurity company developing an advanced artificial intelligence system known as ARCHIVE.\n\n" +
        "Your mission is to infiltrate Helix Dynamics as a new employee, gather evidence, earn the trust of key personnel, and identify the individual responsible for exposing intelligence agents.\n\n" +
        "Be careful. Every action affects how others perceive you. Raise too much suspicion and your cover will be blown.\n\n" +
        "Find the truth. Uncover the culprit. Protect the agency.";

    public static bool IsIntroActive { get; private set; }

    [Header("Scene")]
    [SerializeField] private string gameplaySceneName = "GameScene";

    [Header("UI References")]
    [SerializeField] private CanvasGroup introPanel;
    [SerializeField] private TextMeshProUGUI introTextField;
    [SerializeField] private Button continueButton;

    [Header("Typing")]
    [TextArea(8, 16)]
    [SerializeField] private string introText = DefaultIntroText;
    [SerializeField] private float charactersPerSecond = 45f;

    [Header("Player References")]
    [SerializeField] private FirstPersonController playerController;
    [Tooltip("Optional separate camera/look controller. Leave empty when FirstPersonController owns camera look.")]
    [SerializeField] private MonoBehaviour cameraController;
    [SerializeField] private PlayerInteractor interactionController;

    private Coroutine typingRoutine;
    private bool typingComplete;
    private bool introStarted;
    private static int lastEnsuredGameSceneHandle = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        IsIntroActive = false;
        lastEnsuredGameSceneHandle = -1;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadHandler()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureIntroForGameplayScene()
    {
        EnsureIntroForScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureIntroForScene(scene);
    }

    private static void EnsureIntroForScene(Scene scene)
    {
        if (scene.name != "GameScene")
        {
            return;
        }

        IntroNarrativeController existing =
            FindFirstObjectByType<IntroNarrativeController>(FindObjectsInactive.Include);
        if (existing != null && lastEnsuredGameSceneHandle == scene.handle)
        {
            return;
        }

        lastEnsuredGameSceneHandle = scene.handle;
        Debug.Log("Game Scene Loaded");

        if (existing != null)
        {
            if (!existing.gameObject.activeSelf)
            {
                existing.gameObject.SetActive(true);
            }

            if (!existing.enabled)
            {
                existing.enabled = true;
            }

            return;
        }

        CreateRuntimeIntro();
    }

    private void Awake()
    {
        Debug.Log("Intro Manager Awake", this);
        ResolveReferences();
        WireButtons();
    }

    private void Start()
    {
        Debug.Log("Intro Manager Start", this);
        if (SceneManager.GetActiveScene().name == gameplaySceneName)
        {
            StartIntro();
        }
    }

    private void Update()
    {
        if (!IsIntroActive || typingComplete)
        {
            return;
        }

        if (WasSkipPressed())
        {
            CompleteTypingImmediately();
        }
    }

    private void OnDestroy()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(HandleContinuePressed);
        }

        if (IsIntroActive)
        {
            IsIntroActive = false;
        }
    }

    public void RuntimeSetReferences(CanvasGroup panel, TextMeshProUGUI textField, Button button)
    {
        introPanel = panel;
        introTextField = textField;
        continueButton = button;
        WireButtons();
        SetPanelVisible(false);
    }

    public void StartIntro()
    {
        if (introStarted)
        {
            return;
        }

        introStarted = true;
        IsIntroActive = true;
        typingComplete = false;
        SetGameplayEnabled(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetPanelVisible(true);

        if (continueButton != null)
        {
            continueButton.interactable = false;
        }

        if (introTextField != null)
        {
            introTextField.text = string.IsNullOrWhiteSpace(introText) ? DefaultIntroText : introText;
            introTextField.maxVisibleCharacters = 0;
        }

        Debug.Log("Intro Started", this);
        typingRoutine = StartCoroutine(TypeIntroText());
    }

    private IEnumerator TypeIntroText()
    {
        Debug.Log("Typewriter Started", this);
        string text = string.IsNullOrWhiteSpace(introText) ? DefaultIntroText : introText;
        float delay = 1f / Mathf.Max(1f, charactersPerSecond);

        if (introTextField != null)
        {
            introTextField.text = text;
            introTextField.maxVisibleCharacters = 0;
        }

        for (int i = 0; i < text.Length; i++)
        {
            if (introTextField != null)
            {
                introTextField.maxVisibleCharacters = i + 1;
            }

            yield return new WaitForSecondsRealtime(delay);
        }

        FinishTyping();
    }

    private void CompleteTypingImmediately()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        if (introTextField != null)
        {
            introTextField.text = string.IsNullOrWhiteSpace(introText) ? DefaultIntroText : introText;
            introTextField.maxVisibleCharacters = int.MaxValue;
        }

        FinishTyping();
    }

    private void FinishTyping()
    {
        if (typingComplete)
        {
            return;
        }

        typingComplete = true;
        if (continueButton != null)
        {
            continueButton.interactable = true;
        }

        Debug.Log("Intro Typing Complete", this);
    }

    private void HandleContinuePressed()
    {
        if (!typingComplete)
        {
            CompleteTypingImmediately();
            return;
        }

        Debug.Log("Continue Pressed", this);
        IsIntroActive = false;
        SetPanelVisible(false);
        if (introTextField != null)
        {
            introTextField.maxVisibleCharacters = int.MaxValue;
        }
        SetGameplayEnabled(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("Gameplay Enabled", this);
    }

    private void SetGameplayEnabled(bool enabled)
    {
        if (playerController != null)
        {
            playerController.enabled = enabled;
        }

        if (cameraController != null && cameraController != playerController)
        {
            cameraController.enabled = enabled;
        }

        if (interactionController != null)
        {
            interactionController.enabled = enabled;
        }
    }

    private void SetPanelVisible(bool visible)
    {
        if (visible && !gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (introPanel != null)
        {
            introPanel.alpha = visible ? 1f : 0f;
            introPanel.blocksRaycasts = visible;
            introPanel.interactable = visible;
            if (visible)
            {
                Debug.Log("Intro UI Activated", this);
            }

            return;
        }

        gameObject.SetActive(visible);
        if (visible)
        {
            Debug.Log("Intro UI Activated", this);
        }
    }

    private void ResolveReferences()
    {
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
        }

        if (interactionController == null)
        {
            interactionController = FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        }
    }

    private void WireButtons()
    {
        if (continueButton == null)
        {
            return;
        }

        continueButton.onClick.RemoveListener(HandleContinuePressed);
        continueButton.onClick.AddListener(HandleContinuePressed);
    }

    private static bool WasSkipPressed()
    {
        return (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
               (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
               (Pointer.current != null && Pointer.current.press.wasPressedThisFrame);
    }

    private static void CreateRuntimeIntro()
    {
        Canvas canvas = ResolveCanvas();
        if (canvas == null)
        {
            return;
        }

        EnsureEventSystem();

        GameObject root = new GameObject("IntroNarrativePanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        root.transform.SetParent(canvas.transform, false);
        root.transform.SetAsLastSibling();
        StretchFull(root.GetComponent<RectTransform>());

        Image backdrop = root.GetComponent<Image>();
        backdrop.color = new Color(0.01f, 0.018f, 0.028f, 0.96f);
        backdrop.raycastTarget = true;

        CanvasGroup panelGroup = root.GetComponent<CanvasGroup>();
        panelGroup.alpha = 0f;
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable = false;

        GameObject panel = new GameObject("BriefingPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(root.transform, false);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(980f, 720f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.035f, 0.055f, 0.08f, 0.98f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(42, 42, 38, 34);
        layout.spacing = 18f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI text = CreateLabel(panel.transform, "IntroText", string.Empty, 22f, FontStyles.Normal);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        LayoutElement textLayout = text.gameObject.AddComponent<LayoutElement>();
        textLayout.preferredHeight = 560f;
        textLayout.flexibleHeight = 1f;

        Button button = CreateButton(panel.transform, "ContinueButton", "CONTINUE");
        LayoutElement buttonLayout = button.gameObject.AddComponent<LayoutElement>();
        buttonLayout.preferredHeight = 56f;
        buttonLayout.minHeight = 56f;

        IntroNarrativeController controller = root.AddComponent<IntroNarrativeController>();
        controller.RuntimeSetReferences(panelGroup, text, button);
    }

    private static Canvas ResolveCanvas()
    {
        SpyGameUiRootMarker marker = FindAnyObjectByType<SpyGameUiRootMarker>();
        if (marker != null && marker.TryGetComponent(out Canvas markedCanvas))
        {
            return markedCanvas;
        }

        GameObject named = GameObject.Find("SpyGame_MainCanvas");
        if (named != null && named.TryGetComponent(out Canvas namedCanvas))
        {
            return namedCanvas;
        }

        Canvas existing = FindFirstObjectByType<Canvas>();
        if (existing != null)
        {
            return existing;
        }

        GameObject canvasGo = new GameObject("SpyGame_MainCanvas", typeof(RectTransform));
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float fontSize, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.color = Color.white;
        return label;
    }

    private static Button CreateButton(Transform parent, string name, string text)
    {
        GameObject buttonGo = DefaultControls.CreateButton(CreateUiResources());
        buttonGo.name = name;
        buttonGo.transform.SetParent(parent, false);

        Text legacyText = buttonGo.GetComponentInChildren<Text>();
        if (legacyText != null)
        {
            Destroy(legacyText);
        }

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(buttonGo.transform, false);
        StretchFull(textGo.GetComponent<RectTransform>());

        TextMeshProUGUI label = textGo.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = 16f;
        label.fontStyle = FontStyles.Bold;
        label.characterSpacing = 2f;
        label.color = LightButtonTextColor;
        label.alignment = TextAlignmentOptions.Center;

        return buttonGo.GetComponent<Button>();
    }

    private static DefaultControls.Resources CreateUiResources()
    {
        Sprite fallback = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        return new DefaultControls.Resources
        {
            standard = fallback,
            background = fallback,
            inputField = fallback,
            knob = fallback,
            checkmark = fallback,
            dropdown = fallback,
            mask = fallback
        };
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
