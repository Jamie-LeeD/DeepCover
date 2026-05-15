using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Root HUD coordinator: crosshair, objective line, and quick access to other HUD widgets.
/// Wire the player <see cref="PlayerInteractor"/> prompt reference here or on the interactor directly.
/// </summary>
public class HUDManager : MonoBehaviour
{
    [Header("Crosshair")]
    [SerializeField] private Image crosshairImage;

    [Header("Objective")]
    [SerializeField] private TextMeshProUGUI objectiveText;
    [Tooltip("Shown when no objective is set; keep short.")]
    [SerializeField] private string objectivePlaceholder = "// OBJECTIVE PENDING";

    [Header("Linked widgets")]
    [SerializeField] private InteractionPromptUI interactionPrompt;
    [SerializeField] private SuspicionUIController suspicionUi;
    [SerializeField] private EvidenceNotificationUI evidenceNotification;

    public InteractionPromptUI InteractionPrompt => interactionPrompt;
    public SuspicionUIController SuspicionUi => suspicionUi;
    public EvidenceNotificationUI EvidenceNotification => evidenceNotification;

    private void Awake()
    {
        if (objectiveText != null && string.IsNullOrWhiteSpace(objectiveText.text))
        {
            objectiveText.text = objectivePlaceholder;
        }

        if (crosshairImage != null)
        {
            crosshairImage.raycastTarget = false;
        }
    }

    /// <summary>
    /// Updates the top-center mission line (spy briefing style).
    /// </summary>
    public void SetObjective(string text)
    {
        if (objectiveText == null)
        {
            return;
        }

        objectiveText.text = string.IsNullOrWhiteSpace(text) ? objectivePlaceholder : text;
    }

    /// <summary>
    /// Toggles crosshair visibility (cutscenes, menus).
    /// </summary>
    public void SetCrosshairVisible(bool visible)
    {
        if (crosshairImage != null)
        {
            crosshairImage.enabled = visible;
        }
    }
}
