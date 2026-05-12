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

    private Coroutine revealRoutine;
    private bool isRevealing;
    private Action revealCompletedCallback;

    public bool IsRevealing => isRevealing;

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
    }

    public void DisplayLine(string speakerName, string lineText, Action onRevealComplete)
    {
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
