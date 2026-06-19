using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Pause overlay UI. Resume/Main Menu delegate to <see cref="GameplayPauseController"/>;
/// Controls panel switching stays local so it never changes pause state.
/// </summary>
[DisallowMultipleComponent]
public class PauseMenuView : MonoBehaviour
{
    private static readonly Color LightButtonTextColor = new Color(0f, 0.2f, 0.4f, 1f);

    [Header("UI")]
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private GameObject controlsMenuPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button controlsButton;
    [FormerlySerializedAs("restartButton")]
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button closeControlsButton;
    [HideInInspector] [SerializeField] private Button exitButton;
    [SerializeField] private TextMeshProUGUI titleLabel;

    private GameplayPauseController controller;

    private void Awake()
    {
        controller = GetComponentInParent<GameplayPauseController>();
        if (controller == null)
        {
            controller = FindFirstObjectByType<GameplayPauseController>();
        }

        if (!HasConfiguredUiReferences())
        {
            return;
        }

        ApplyStaticLabels();
        EnsureControlsUi();
        WireButtons();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        UnwireButtons();
    }

    public void SetVisible(bool visible)
    {
        if (visible && !gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (rootGroup != null)
        {
            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.blocksRaycasts = visible;
            rootGroup.interactable = visible;
            if (visible)
            {
                ShowPausePanel();
            }
            else
            {
                ShowControlsPanel(false);
            }

            return;
        }

        gameObject.SetActive(visible);
        if (visible)
        {
            ShowPausePanel();
        }
    }

    public bool IsVisible => rootGroup != null && rootGroup.blocksRaycasts;

    private bool HasConfiguredUiReferences()
    {
        return rootGroup != null ||
               pauseMenuPanel != null ||
               controlsMenuPanel != null ||
               resumeButton != null ||
               controlsButton != null ||
               mainMenuButton != null ||
               closeControlsButton != null ||
               titleLabel != null;
    }

    private void WireButtons()
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(OnResumeClicked);
            resumeButton.onClick.AddListener(OnResumeClicked);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(OnMainMenuClicked);
            exitButton.onClick.AddListener(OnMainMenuClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        if (controlsButton != null)
        {
            controlsButton.onClick.RemoveListener(OnControlsClicked);
            controlsButton.onClick.AddListener(OnControlsClicked);
        }

        if (closeControlsButton != null)
        {
            closeControlsButton.onClick.RemoveListener(OnCloseControlsClicked);
            closeControlsButton.onClick.AddListener(OnCloseControlsClicked);
        }
    }

    private void ApplyStaticLabels()
    {
        if (titleLabel != null)
        {
            titleLabel.text = "PAUSED";
        }

        SetButtonLabel(resumeButton, "RESUME");
        SetButtonLabel(controlsButton, "CONTROLS");
        SetButtonLabel(mainMenuButton, "MAIN MENU");
        SetButtonLabel(exitButton, "MAIN MENU");
        SetButtonLabel(closeControlsButton, "X");
    }

    private void UnwireButtons()
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(OnResumeClicked);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(OnMainMenuClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
        }

        if (controlsButton != null)
        {
            controlsButton.onClick.RemoveListener(OnControlsClicked);
        }

        if (closeControlsButton != null)
        {
            closeControlsButton.onClick.RemoveListener(OnCloseControlsClicked);
        }
    }

    private void OnResumeClicked()
    {
        controller?.ResumeGame();
    }

    private void OnMainMenuClicked()
    {
        controller?.LoadMainMenu();
    }

    private void OnControlsClicked()
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        ShowControlsPanel(true);
        Debug.Log("Controls Menu Opened", this);
    }

    private void OnCloseControlsClicked()
    {
        ShowPausePanel();
        Debug.Log("Controls Menu Closed", this);
        Debug.Log("Returned to Pause Menu", this);
    }

    private void ShowPausePanel()
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }

        ShowControlsPanel(false);
    }

    private void ShowControlsPanel(bool visible)
    {
        if (controlsMenuPanel != null)
        {
            controlsMenuPanel.SetActive(visible);
        }
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        TextMeshProUGUI tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = label;
        }
    }

    private void EnsureControlsUi()
    {
        ResolvePauseMenuPanel();

        if (controlsButton == null && pauseMenuPanel != null)
        {
            Transform existingControls = pauseMenuPanel.transform.Find("ControlsButton");
            if (existingControls != null)
            {
                controlsButton = existingControls.GetComponent<Button>();
            }

            if (controlsButton == null)
            {
                controlsButton = CreateButton(pauseMenuPanel.transform, "ControlsButton", "CONTROLS");
                LayoutElement layout = controlsButton.gameObject.AddComponent<LayoutElement>();
                layout.minHeight = 44f;
                layout.preferredHeight = 54f;
                if (mainMenuButton != null)
                {
                    controlsButton.transform.SetSiblingIndex(mainMenuButton.transform.GetSiblingIndex());
                }
            }
        }

        if (controlsMenuPanel == null)
        {
            Transform existingPanel = transform.Find("ControlsMenuPanel");
            controlsMenuPanel = existingPanel != null ? existingPanel.gameObject : BuildControlsPanel();
        }

        if (closeControlsButton == null && controlsMenuPanel != null)
        {
            Transform existingClose = controlsMenuPanel.transform.Find("CloseControlsButton");
            closeControlsButton = existingClose != null
                ? existingClose.GetComponent<Button>()
                : controlsMenuPanel.GetComponentInChildren<Button>(true);
        }

        ShowControlsPanel(false);
    }

    private void ResolvePauseMenuPanel()
    {
        if (pauseMenuPanel != null)
        {
            return;
        }

        if (titleLabel != null)
        {
            pauseMenuPanel = titleLabel.transform.parent.gameObject;
            return;
        }

        if (resumeButton != null)
        {
            pauseMenuPanel = resumeButton.transform.parent.gameObject;
            return;
        }

        if (mainMenuButton != null)
        {
            pauseMenuPanel = mainMenuButton.transform.parent.gameObject;
        }
    }

    private GameObject BuildControlsPanel()
    {
        Transform parent = pauseMenuPanel != null && pauseMenuPanel.transform.parent != null
            ? pauseMenuPanel.transform.parent
            : transform;

        GameObject panel = new GameObject("ControlsMenuPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(560f, 460f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.05f, 0.08f, 0.12f, 0.96f);

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.62f, 0.72f, 0.5f);
        outline.effectDistance = new Vector2(2f, -2f);

        TextMeshProUGUI title = CreateLabel(panel.transform, "ControlsTitle", "CONTROLS", 28f, FontStyles.Bold);
        RectTransform titleRt = title.GetComponent<RectTransform>();
        titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -34f);
        titleRt.sizeDelta = new Vector2(460f, 48f);
        title.characterSpacing = 4f;
        title.color = new Color(0.75f, 0.88f, 0.92f, 1f);

        TextMeshProUGUI controls = CreateLabel(
            panel.transform,
            "ControlsList",
            "W - Move Forward\n" +
            "A - Move Left\n" +
            "S - Move Backward\n" +
            "D - Move Right\n\n" +
            "E - Interact with Objects and NPCs\n" +
            "I - Open Evidence Journal\n" +
            "ESC - Open/Close Pause Menu\n" +
            "Mouse Movement - Look Around",
            18f,
            FontStyles.Normal);
        RectTransform controlsRt = controls.GetComponent<RectTransform>();
        controlsRt.anchorMin = controlsRt.anchorMax = new Vector2(0.5f, 0.5f);
        controlsRt.pivot = new Vector2(0.5f, 0.5f);
        controlsRt.anchoredPosition = new Vector2(0f, -16f);
        controlsRt.sizeDelta = new Vector2(460f, 300f);
        controls.alignment = TextAlignmentOptions.Left;
        controls.color = new Color(0.92f, 0.95f, 0.98f, 1f);
        controls.textWrappingMode = TextWrappingModes.Normal;

        closeControlsButton = CreateButton(panel.transform, "CloseControlsButton", "X");
        RectTransform closeRt = closeControlsButton.GetComponent<RectTransform>();
        closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-16f, -16f);
        closeRt.sizeDelta = new Vector2(42f, 36f);

        panel.SetActive(false);
        return panel;
    }

    private static TextMeshProUGUI CreateLabel(
        Transform parent,
        string name,
        string text,
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
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        return tmp;
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        GameObject buttonGo = DefaultControls.CreateButton(CreateUiResources());
        buttonGo.name = name;
        buttonGo.transform.SetParent(parent, false);

        Text legacyText = buttonGo.GetComponentInChildren<Text>();
        if (legacyText != null)
        {
            Destroy(legacyText);
        }

        GameObject labelGo = new GameObject("Text", typeof(RectTransform));
        labelGo.transform.SetParent(buttonGo.transform, false);
        RectTransform labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = label;
        tmp.fontSize = label == "X" ? 18f : 16f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = LightButtonTextColor;
        tmp.alignment = TextAlignmentOptions.Center;

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

    public void RuntimeSetReferences(
        CanvasGroup group,
        GameObject pausePanel,
        GameObject controlsPanel,
        Button resume,
        Button controls,
        Button mainMenu,
        Button closeControls,
        TextMeshProUGUI title)
    {
        rootGroup = group;
        pauseMenuPanel = pausePanel;
        controlsMenuPanel = controlsPanel;
        resumeButton = resume;
        controlsButton = controls;
        mainMenuButton = mainMenu;
        closeControlsButton = closeControls;
        titleLabel = title;
        ApplyStaticLabels();
        EnsureControlsUi();
        WireButtons();
        SetVisible(false);
    }

    public void RuntimeSetReferences(
        CanvasGroup group,
        Button resume,
        Button mainMenu,
        TextMeshProUGUI title)
    {
        RuntimeSetReferences(group, null, null, resume, null, mainMenu, null, title);
    }

#if UNITY_EDITOR
    public void EditorSetReferences(
        CanvasGroup group,
        GameObject pausePanel,
        GameObject controlsPanel,
        Button resume,
        Button controls,
        Button mainMenu,
        Button closeControls,
        TextMeshProUGUI title)
    {
        rootGroup = group;
        pauseMenuPanel = pausePanel;
        controlsMenuPanel = controlsPanel;
        resumeButton = resume;
        controlsButton = controls;
        mainMenuButton = mainMenu;
        closeControlsButton = closeControls;
        exitButton = null;
        titleLabel = title;
    }

    public void EditorSetReferences(
        CanvasGroup group,
        Button resume,
        Button mainMenu,
        TextMeshProUGUI title)
    {
        EditorSetReferences(group, null, null, resume, null, mainMenu, null, title);
    }
#endif
}
