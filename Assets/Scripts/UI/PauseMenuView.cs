using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Pause overlay UI (Resume / Main Menu). Visibility only — logic lives on <see cref="GameplayPauseController"/>.
/// </summary>
[DisallowMultipleComponent]
public class PauseMenuView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private Button resumeButton;
    [FormerlySerializedAs("restartButton")]
    [SerializeField] private Button mainMenuButton;
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

        ApplyStaticLabels();
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
            return;
        }

        gameObject.SetActive(visible);
    }

    public bool IsVisible => rootGroup != null && rootGroup.blocksRaycasts;

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
    }

    private void ApplyStaticLabels()
    {
        if (titleLabel != null)
        {
            titleLabel.text = "PAUSED";
        }

        SetButtonLabel(resumeButton, "RESUME");
        SetButtonLabel(mainMenuButton, "MAIN MENU");
        SetButtonLabel(exitButton, "MAIN MENU");
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
    }

    private void OnResumeClicked()
    {
        controller?.ResumeGame();
    }

    private void OnMainMenuClicked()
    {
        controller?.LoadMainMenu();
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

    public void RuntimeSetReferences(
        CanvasGroup group,
        Button resume,
        Button mainMenu,
        TextMeshProUGUI title)
    {
        rootGroup = group;
        resumeButton = resume;
        mainMenuButton = mainMenu;
        titleLabel = title;
        ApplyStaticLabels();
        WireButtons();
        SetVisible(false);
    }

#if UNITY_EDITOR
    public void EditorSetReferences(
        CanvasGroup group,
        Button resume,
        Button mainMenu,
        TextMeshProUGUI title)
    {
        rootGroup = group;
        resumeButton = resume;
        mainMenuButton = mainMenu;
        exitButton = null;
        titleLabel = title;
    }
#endif
}
