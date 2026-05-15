using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TextMeshPro dialogue panel with a typewriter reveal and continue control.
/// </summary>
public class DialogueUIView : MonoBehaviour
{
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI dialogueBodyText;
    [SerializeField] private Button continueButton;
    [SerializeField] private float charactersPerSecond = 40f;
    [SerializeField] private CanvasGroup evidenceCanvasGroup;
    [SerializeField] private Image evidenceIcon;
    [SerializeField] private TextMeshProUGUI evidenceNameText;
    [SerializeField] private TextMeshProUGUI evidenceCategoryText;
    [SerializeField] private TextMeshProUGUI evidenceDescriptionText;

    private Coroutine revealRoutine;
    private bool isRevealing;
    private bool isStreamingLine;
    private Action revealCompletedCallback;

    public bool IsRevealing => isRevealing;
    public bool IsStreamingLine => isStreamingLine;

    private void Awake()
    {
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(HandleContinuePressed);
        }

        SetPanelVisible(false);
    }

    private void OnDestroy()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(HandleContinuePressed);
        }
    }

    public event Action ContinueRequested;

    public void SetPanelVisible(bool visible)
    {
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = visible ? 1f : 0f;
            panelCanvasGroup.blocksRaycasts = visible;
            panelCanvasGroup.interactable = visible;
        }
        else
        {
            gameObject.SetActive(visible);
        }

        if (!visible)
        {
            SetEvidenceOverlay(null);
        }
    }

    /// <summary>
    /// Opens the panel for live Ollama tokens (no typewriter; text grows as chunks arrive).
    /// </summary>
    public void BeginStreamingLine(string speakerName, EvidenceData evidenceOverlay = null)
    {
        StopRevealRoutine();
        isStreamingLine = true;
        isRevealing = false;
        revealCompletedCallback = null;

        SetEvidenceOverlay(evidenceOverlay);

        if (speakerNameText != null)
        {
            speakerNameText.text = speakerName;
        }

        if (dialogueBodyText != null)
        {
            dialogueBodyText.text = string.Empty;
            dialogueBodyText.ForceMeshUpdate();
            dialogueBodyText.maxVisibleCharacters = int.MaxValue;
        }
    }

    /// <summary>Appends a streamed token chunk to the body (main thread).</summary>
    public void AppendStreamingText(string chunk)
    {
        if (!isStreamingLine || dialogueBodyText == null || string.IsNullOrEmpty(chunk))
        {
            return;
        }

        dialogueBodyText.text += chunk;
        dialogueBodyText.ForceMeshUpdate();
    }

    /// <summary>Ends streaming; optionally replaces text with a formatted final string.</summary>
    public void EndStreamingLine(string finalText, Action onComplete)
    {
        if (!isStreamingLine)
        {
            onComplete?.Invoke();
            return;
        }

        isStreamingLine = false;

        if (dialogueBodyText != null && finalText != null)
        {
            dialogueBodyText.text = finalText;
            dialogueBodyText.ForceMeshUpdate();
            dialogueBodyText.maxVisibleCharacters = int.MaxValue;
        }

        revealCompletedCallback = onComplete;
        revealCompletedCallback?.Invoke();
        revealCompletedCallback = null;
    }

    /// <summary>Clears streaming state when dialogue is closed mid-stream.</summary>
    public void CancelStreamingLine()
    {
        isStreamingLine = false;
        isRevealing = false;
        StopRevealRoutine();
        revealCompletedCallback = null;
    }

    public void DisplayLine(string speakerName, string lineText, Action onRevealComplete, EvidenceData evidenceOverlay = null)
    {
        SetEvidenceOverlay(evidenceOverlay);

        if (speakerNameText != null)
        {
            speakerNameText.text = speakerName;
        }

        revealCompletedCallback = onRevealComplete;
        StartReveal(lineText ?? string.Empty);
    }

    public void CompleteReveal()
    {
        if (!isRevealing)
        {
            return;
        }

        StopRevealRoutine();

        if (dialogueBodyText != null)
        {
            dialogueBodyText.maxVisibleCharacters = int.MaxValue;
        }

        isRevealing = false;
        revealCompletedCallback?.Invoke();
        revealCompletedCallback = null;
    }

    private void HandleContinuePressed()
    {
        ContinueRequested?.Invoke();
    }

    /// <summary>
    /// Shows or hides the optional evidence card during dialogue (e.g. presenting a clue to an NPC).
    /// </summary>
    public void SetEvidenceOverlay(EvidenceData evidence)
    {
        bool hasEvidence = evidence != null;

        if (evidenceCanvasGroup != null)
        {
            evidenceCanvasGroup.alpha = hasEvidence ? 1f : 0f;
            evidenceCanvasGroup.blocksRaycasts = false;
            evidenceCanvasGroup.interactable = false;
        }

        if (!hasEvidence)
        {
            if (evidenceIcon != null)
            {
                evidenceIcon.sprite = null;
                evidenceIcon.enabled = false;
            }

            if (evidenceNameText != null)
            {
                evidenceNameText.text = string.Empty;
            }

            if (evidenceCategoryText != null)
            {
                evidenceCategoryText.text = string.Empty;
            }

            if (evidenceDescriptionText != null)
            {
                evidenceDescriptionText.text = string.Empty;
            }

            return;
        }

        if (evidenceIcon != null)
        {
            evidenceIcon.sprite = evidence.Icon;
            evidenceIcon.enabled = evidence.Icon != null;
        }

        if (evidenceNameText != null)
        {
            evidenceNameText.text = evidence.DisplayName;
        }

        if (evidenceCategoryText != null)
        {
            evidenceCategoryText.text = evidence.Category.ToString();
        }

        if (evidenceDescriptionText != null)
        {
            evidenceDescriptionText.text = evidence.Description;
        }
    }

    private void StartReveal(string lineText)
    {
        StopRevealRoutine();

        if (dialogueBodyText == null)
        {
            isRevealing = false;
            revealCompletedCallback?.Invoke();
            revealCompletedCallback = null;
            return;
        }

        dialogueBodyText.text = lineText;
        dialogueBodyText.ForceMeshUpdate();
        dialogueBodyText.maxVisibleCharacters = 0;
        revealRoutine = StartCoroutine(RevealCharacters());
    }

    private IEnumerator RevealCharacters()
    {
        isRevealing = true;
        int visibleCharacters = 0;
        int totalCharacters = dialogueBodyText.textInfo.characterCount;
        float delay = charactersPerSecond <= 0f ? 0f : 1f / charactersPerSecond;

        while (visibleCharacters < totalCharacters)
        {
            visibleCharacters++;
            dialogueBodyText.maxVisibleCharacters = visibleCharacters;

            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
            else
            {
                yield return null;
            }
        }

        isRevealing = false;
        revealCompletedCallback?.Invoke();
        revealCompletedCallback = null;
        revealRoutine = null;
    }

    private void StopRevealRoutine()
    {
        if (revealRoutine == null)
        {
            return;
        }

        StopCoroutine(revealRoutine);
        revealRoutine = null;
    }
}
