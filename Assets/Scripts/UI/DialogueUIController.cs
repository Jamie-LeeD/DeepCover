using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Groups dialogue-adjacent controls (evidence actions) separate from <see cref="DialogueUIView"/> line rendering.
/// </summary>
public class DialogueUIController : MonoBehaviour
{
    [Header("Core view")]
    [SerializeField] private DialogueUIView dialogueView;

    [Header("Evidence actions")]
    [Tooltip("Optional buttons near the dialogue chrome (e.g. open evidence journal). Wire in inspector.")]
    [SerializeField] private Button evidenceJournalButton;
    [SerializeField] private UnityEvent onEvidenceJournalClicked;

    [Header("Accusation")]
    [SerializeField] private Button accuseButton;
    [SerializeField] private CanvasGroup accusationConfirmGroup;
    [SerializeField] private TextMeshProUGUI accusationConfirmText;
    [SerializeField] private Button confirmAccuseButton;
    [SerializeField] private Button cancelAccuseButton;

    private NPCBrain pendingAccusationTarget;
    private DialogueQuestionInputView questionInputView;
    private RectTransform confirmationButtonRow;

    public DialogueUIView DialogueView => dialogueView;

    private void Awake()
    {
        EnsureAccusationControls();
        ResolveQuestionInputView();

        if (evidenceJournalButton != null)
        {
            evidenceJournalButton.onClick.AddListener(HandleEvidenceJournalClicked);
        }

        WireAccusationButtons();
        SetAccuseVisible(false);
        SetConfirmationVisible(false);
    }

    private void OnEnable()
    {
        SubscribeDialogueEvents();
    }

    private void Start()
    {
        SubscribeDialogueEvents();
    }

    private void Update()
    {
        if (!IsConfirmationVisible())
        {
            UpdateAccuseAvailability();
        }

        HandleConfirmationManualClick();
    }

    private void OnDestroy()
    {
        if (evidenceJournalButton != null)
        {
            evidenceJournalButton.onClick.RemoveListener(HandleEvidenceJournalClicked);
        }

        UnwireAccusationButtons();

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.DialogueStarted -= HandleDialogueStateChanged;
            DialogueManager.Instance.DialogueEnded -= HandleDialogueEnded;
        }
    }

    private void HandleEvidenceJournalClicked()
    {
        onEvidenceJournalClicked?.Invoke();

        if (onEvidenceJournalClicked != null && onEvidenceJournalClicked.GetPersistentEventCount() > 0)
        {
            return;
        }

        EvidenceInventoryUI journal = FindFirstObjectByType<EvidenceInventoryUI>(FindObjectsInactive.Include);
        if (journal != null)
        {
            if (journal.IsOpen)
            {
                journal.CloseJournal();
            }
            else
            {
                journal.OpenJournalFromConversation();
            }
        }
    }

    private void HandleAccuseClicked()
    {
        pendingAccusationTarget = NPCBrain.CurrentEvidenceInquiryTarget;
        if (pendingAccusationTarget == null)
        {
            return;
        }

        if (accusationConfirmText != null)
        {
            accusationConfirmText.text = "Are you ready to make your accusation?";
        }

        SetConfirmationVisible(true);
    }

    private void HandleConfirmAccuseClicked()
    {
        NPCBrain target = pendingAccusationTarget ?? NPCBrain.CurrentEvidenceInquiryTarget;
        SetConfirmationVisible(false);

        if (target == null || GameStateManager.Instance == null)
        {
            return;
        }

        GameStateManager.Instance.Accuse(target.Profile);
    }

    private void HandleCancelAccuseClicked()
    {
        pendingAccusationTarget = null;
        SetConfirmationVisible(false);
    }

    private void SubscribeDialogueEvents()
    {
        if (DialogueManager.Instance == null)
        {
            return;
        }

        DialogueManager.Instance.DialogueStarted -= HandleDialogueStateChanged;
        DialogueManager.Instance.DialogueStarted += HandleDialogueStateChanged;
        DialogueManager.Instance.DialogueEnded -= HandleDialogueEnded;
        DialogueManager.Instance.DialogueEnded += HandleDialogueEnded;
    }

    private void HandleDialogueStateChanged()
    {
        UpdateAccuseAvailability();
    }

    private void HandleDialogueEnded()
    {
        pendingAccusationTarget = null;
        SetConfirmationVisible(false);
        SetAccuseVisible(false);
    }

    private void UpdateAccuseAvailability()
    {
        bool dialogueActive = DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive;
        ResolveQuestionInputView();
        bool questionInputActive = questionInputView != null && questionInputView.IsSessionActive;
        bool conversationActive = dialogueActive || questionInputActive;
        NPCBrain target = NPCBrain.CurrentEvidenceInquiryTarget;
        bool investigationComplete = false;
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.GetInvestigationProgress(
                out _,
                out _,
                out _,
                out investigationComplete);
        }

        bool terminalState = GameStateManager.Instance != null && GameStateManager.Instance.IsTerminalState;
        bool canAccuse = conversationActive &&
                         investigationComplete &&
                         !terminalState &&
                         target != null &&
                         target.IsAccusable;

        SetAccuseVisible(canAccuse);

        if (!canAccuse)
        {
            pendingAccusationTarget = null;
            SetConfirmationVisible(false);
        }
    }

    private void SetAccuseVisible(bool visible)
    {
        bool buttonExists = accuseButton != null;
        bool wasActive = buttonExists && accuseButton.gameObject.activeSelf;
        if (accuseButton == null)
        {
            Debug.LogWarning("[AccusationUI] Accuse button missing; cannot set visibility.", this);
            return;
        }

        accuseButton.gameObject.SetActive(visible);
        accuseButton.interactable = visible;
    }

    private void SetConfirmationVisible(bool visible)
    {
        if (accusationConfirmGroup == null)
        {
            return;
        }

        if (visible)
        {
            transform.SetAsLastSibling();
            accusationConfirmGroup.transform.SetAsLastSibling();
        }

        ResolveQuestionInputView();
        if (questionInputView != null)
        {
            questionInputView.SetRaycastBlocking(!visible);
        }

        accusationConfirmGroup.alpha = visible ? 1f : 0f;
        accusationConfirmGroup.blocksRaycasts = visible;
        accusationConfirmGroup.interactable = visible;
    }

    private void HandleConfirmationManualClick()
    {
        if (!IsConfirmationVisible())
        {
            return;
        }

        if (WasConfirmPressed())
        {
            HandleConfirmAccuseClicked();
            return;
        }

        if (WasCancelPressed())
        {
            HandleCancelAccuseClicked();
            return;
        }

        if (!WasPrimaryMousePressed(out Vector2 screenPosition))
        {
            return;
        }

        Camera uiCamera = ResolveUiCamera();

        if (IsScreenPointInsideButton(confirmAccuseButton, screenPosition, uiCamera))
        {
            HandleConfirmAccuseClicked();
            return;
        }

        if (IsScreenPointInsideButton(cancelAccuseButton, screenPosition, uiCamera))
        {
            HandleCancelAccuseClicked();
            return;
        }

        if (TryHandleButtonRowClick(screenPosition, uiCamera))
        {
            return;
        }
    }

    private bool IsConfirmationVisible()
    {
        return accusationConfirmGroup != null &&
               accusationConfirmGroup.alpha > 0.5f &&
               accusationConfirmGroup.blocksRaycasts;
    }

    private static bool IsScreenPointInsideButton(Button button, Vector2 screenPosition, Camera uiCamera)
    {
        if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
        {
            return false;
        }

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        return buttonRect != null &&
               RectTransformUtility.RectangleContainsScreenPoint(buttonRect, screenPosition, uiCamera);
    }

    private Camera ResolveUiCamera()
    {
        Canvas canvas = accusationConfirmGroup != null
            ? accusationConfirmGroup.GetComponentInParent<Canvas>()
            : null;
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }

    private bool TryHandleButtonRowClick(Vector2 screenPosition, Camera uiCamera)
    {
        if (confirmationButtonRow == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                confirmationButtonRow,
                screenPosition,
                uiCamera,
                out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = confirmationButtonRow.rect;
        if (!rect.Contains(localPoint))
        {
            return false;
        }

        bool confirmSide = localPoint.x < rect.center.x;
        if (confirmSide)
        {
            HandleConfirmAccuseClicked();
        }
        else
        {
            HandleCancelAccuseClicked();
        }

        return true;
    }

    private static bool WasPrimaryMousePressed(out Vector2 screenPosition)
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            screenPosition = Pointer.current.position.ReadValue();
            return true;
        }

        screenPosition = default;
        return false;
    }

    private static bool WasConfirmPressed()
    {
        return Keyboard.current != null &&
               (Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame);
    }

    private static bool WasCancelPressed()
    {
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    private void WireAccusationButtons()
    {
        UnwireAccusationButtons();

        if (accuseButton != null)
        {
            accuseButton.onClick.AddListener(HandleAccuseClicked);
        }

        if (confirmAccuseButton != null)
        {
            confirmAccuseButton.onClick.AddListener(HandleConfirmAccuseClicked);
        }

        if (cancelAccuseButton != null)
        {
            cancelAccuseButton.onClick.AddListener(HandleCancelAccuseClicked);
        }
    }

    private void UnwireAccusationButtons()
    {
        if (accuseButton != null)
        {
            accuseButton.onClick.RemoveListener(HandleAccuseClicked);
        }

        if (confirmAccuseButton != null)
        {
            confirmAccuseButton.onClick.RemoveListener(HandleConfirmAccuseClicked);
        }

        if (cancelAccuseButton != null)
        {
            cancelAccuseButton.onClick.RemoveListener(HandleCancelAccuseClicked);
        }
    }

    private void EnsureAccusationControls()
    {
        if (accuseButton == null)
        {
            accuseButton = CreateRuntimeButton("AccuseButton", "ACCUSE", transform, new Vector2(150f, 42f));
            RectTransform rt = accuseButton.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-20f, 8f);
        }

        if (accusationConfirmGroup != null)
        {
            return;
        }

        Transform modalParent = ResolveModalParent();
        GameObject root = new GameObject(
            "AccusationConfirm",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(Image));
        root.transform.SetParent(modalParent, false);
        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        Canvas modalCanvas = root.GetComponent<Canvas>();
        modalCanvas.overrideSorting = true;
        modalCanvas.sortingOrder = 500;

        Image image = root.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.35f);

        accusationConfirmGroup = root.GetComponent<CanvasGroup>();

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(520f, 160f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.035f, 0.055f, 0.08f, 0.96f);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 18, 18);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        accusationConfirmText = CreateRuntimeLabel(
            "Prompt",
            "Are you ready to make your accusation?",
            panel.transform,
            18f,
            FontStyles.Bold);
        AddLayout(accusationConfirmText.gameObject, 52f);

        GameObject row = new GameObject("Buttons", typeof(RectTransform));
        row.transform.SetParent(panel.transform, false);
        confirmationButtonRow = row.GetComponent<RectTransform>();
        HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 12f;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        AddLayout(row, 46f);

        confirmAccuseButton = CreateRuntimeButton("ConfirmAccuseButton", "CONFIRM", row.transform, new Vector2(160f, 42f));
        cancelAccuseButton = CreateRuntimeButton("CancelAccuseButton", "CANCEL", row.transform, new Vector2(160f, 42f));
    }

    private Transform ResolveModalParent()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null ? canvas.transform : transform;
    }

    private void ResolveQuestionInputView()
    {
        if (questionInputView == null)
        {
            questionInputView = FindFirstObjectByType<DialogueQuestionInputView>(FindObjectsInactive.Include);
        }
    }

    private static Button CreateRuntimeButton(string name, string label, Transform parent, Vector2 size)
    {
        GameObject go = DefaultControls.CreateButton(CreateUiResources());
        go.name = name;
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;

        Text legacyText = go.GetComponentInChildren<Text>();
        if (legacyText != null)
        {
            Destroy(legacyText);
        }

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = label;
        tmp.fontSize = 14f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 1.5f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        return go.GetComponent<Button>();
    }

    private static TextMeshProUGUI CreateRuntimeLabel(
        string name,
        string text,
        Transform parent,
        float fontSize,
        FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    private static void AddLayout(GameObject go, float preferredHeight)
    {
        LayoutElement element = go.AddComponent<LayoutElement>();
        element.minHeight = preferredHeight;
        element.preferredHeight = preferredHeight;
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
}
