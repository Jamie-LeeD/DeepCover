using TMPro;
using UnityEngine;
using UnityEngine.Events;
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

    public DialogueUIView DialogueView => dialogueView;

    private void Awake()
    {
        if (evidenceJournalButton != null)
        {
            evidenceJournalButton.onClick.AddListener(HandleEvidenceJournalClicked);
        }
    }

    private void OnDestroy()
    {
        if (evidenceJournalButton != null)
        {
            evidenceJournalButton.onClick.RemoveListener(HandleEvidenceJournalClicked);
        }
    }

    private void HandleEvidenceJournalClicked()
    {
        onEvidenceJournalClicked?.Invoke();
    }
}
