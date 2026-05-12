using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays and hides the on-screen interaction prompt.
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Text promptText;
    [SerializeField] private string promptFormat = "Press E to {0}";

    private void Awake()
    {
        Hide();
    }

    /// <summary>
    /// Shows the prompt with the supplied interactable message.
    /// </summary>
    public void Show(string interactionPrompt)
    {
        if (promptText != null)
        {
            promptText.text = string.Format(promptFormat, interactionPrompt);
        }

        SetVisible(true);
    }

    /// <summary>
    /// Hides the interaction prompt.
    /// </summary>
    public void Hide()
    {
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
            return;
        }

        if (promptText != null)
        {
            promptText.gameObject.SetActive(visible);
        }
    }
}
