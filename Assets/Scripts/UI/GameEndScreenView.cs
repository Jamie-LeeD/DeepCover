using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared Game Over / Victory overlay. Button behavior delegates to GameStateManager.
/// </summary>
[DisallowMultipleComponent]
public class GameEndScreenView : MonoBehaviour
{
    private enum PrimaryButtonAction
    {
        Retry,
        Quit
    }

    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private TextMeshProUGUI primaryButtonLabel;
    [SerializeField] private Button primaryButton;
    [SerializeField] private Button mainMenuButton;

    private GameStateManager manager;
    private PrimaryButtonAction primaryButtonAction = PrimaryButtonAction.Retry;

    private void Awake()
    {
        ResolveManager();
        WireButtons();
        Hide();
    }

    private void OnDestroy()
    {
        UnwireButtons();
    }

    public void ShowGameOver(string reason)
    {
        primaryButtonAction = PrimaryButtonAction.Retry;
        SetButtonOrder(primaryFirst: true);
        SetText("GAME OVER", string.IsNullOrWhiteSpace(reason) ? "Your cover has been blown." : reason, "RETRY");
        Show();
    }

    public void ShowVictory(string message)
    {
        primaryButtonAction = PrimaryButtonAction.Quit;
        SetButtonOrder(primaryFirst: false);
        SetText(
            "MISSION COMPLETE",
            string.IsNullOrWhiteSpace(message) ? "ARCHIVE has been exposed." : message,
            "QUIT GAME");
        Show();
    }

    public void ShowMissionFailed(string message)
    {
        primaryButtonAction = PrimaryButtonAction.Retry;
        SetButtonOrder(primaryFirst: true);
        SetText(
            "MISSION FAILED",
            string.IsNullOrWhiteSpace(message) ? "You accused the wrong suspect." : message,
            "RETRY");
        Show();
    }

    public void Show()
    {
        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    public void RuntimeSetReferences(
        CanvasGroup group,
        TextMeshProUGUI title,
        TextMeshProUGUI message,
        TextMeshProUGUI primaryLabel,
        Button primary,
        Button mainMenu)
    {
        rootGroup = group;
        titleText = title;
        messageText = message;
        primaryButtonLabel = primaryLabel;
        primaryButton = primary;
        mainMenuButton = mainMenu;
        WireButtons();
        SetVisible(false);
    }

    private void SetText(string title, string message, string primaryLabel)
    {
        if (titleText != null)
        {
            titleText.text = title;
        }

        if (messageText != null)
        {
            messageText.text = message;
        }

        if (primaryButtonLabel != null)
        {
            primaryButtonLabel.text = primaryLabel;
        }
    }

    private void SetVisible(bool visible)
    {
        if (visible && !gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (visible)
        {
            transform.SetAsLastSibling();
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

    private void ResolveManager()
    {
        if (manager == null)
        {
            manager = GetComponentInParent<GameStateManager>();
        }

        if (manager == null)
        {
            manager = FindFirstObjectByType<GameStateManager>();
        }
    }

    private void WireButtons()
    {
        ResolveManager();
        UnwireButtons();

        if (primaryButton != null)
        {
            primaryButton.onClick.AddListener(HandlePrimaryClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(HandleMainMenuClicked);
        }
    }

    private void UnwireButtons()
    {
        if (primaryButton != null)
        {
            primaryButton.onClick.RemoveListener(HandlePrimaryClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(HandleMainMenuClicked);
        }
    }

    private void HandlePrimaryClicked()
    {
        if (manager == null)
        {
            return;
        }

        if (primaryButtonAction == PrimaryButtonAction.Quit)
        {
            manager.QuitGame();
            return;
        }

        manager.RetryCurrentScene();
    }

    private void HandleMainMenuClicked()
    {
        manager?.LoadMainMenu();
    }

    private void SetButtonOrder(bool primaryFirst)
    {
        if (primaryButton == null || mainMenuButton == null)
        {
            return;
        }

        int primaryIndex = primaryButton.transform.GetSiblingIndex();
        int mainMenuIndex = mainMenuButton.transform.GetSiblingIndex();

        if (primaryFirst && primaryIndex > mainMenuIndex)
        {
            primaryButton.transform.SetSiblingIndex(mainMenuIndex);
        }
        else if (!primaryFirst && mainMenuIndex > primaryIndex)
        {
            mainMenuButton.transform.SetSiblingIndex(primaryIndex);
        }
    }
}
