using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Brief on-screen notification when new evidence is added to the inventory.
/// </summary>
public class EvidenceNotificationUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private float visibleSeconds = 2.5f;
    [SerializeField] private float fadeOutSeconds = 0.35f;

    private Coroutine activeRoutine;

    private void OnEnable()
    {
        if (EvidenceInventory.Instance != null)
        {
            EvidenceInventory.Instance.EvidenceCollected += HandleEvidenceCollected;
        }

        HideImmediate();
    }

    private void OnDisable()
    {
        if (EvidenceInventory.Instance != null)
        {
            EvidenceInventory.Instance.EvidenceCollected -= HandleEvidenceCollected;
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }
    }

    private void HandleEvidenceCollected(EvidenceData evidence)
    {
        if (evidence == null)
        {
            return;
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        activeRoutine = StartCoroutine(ShowRoutine(evidence));
    }

    private IEnumerator ShowRoutine(EvidenceData evidence)
    {
        if (titleText != null)
        {
            titleText.text = $"EVIDENCE ACQUIRED: {evidence.DisplayName}";
        }

        if (categoryText != null)
        {
            categoryText.text = evidence.Category.ToString();
        }

        if (iconImage != null)
        {
            iconImage.sprite = evidence.Icon;
            iconImage.enabled = evidence.Icon != null;
        }

        if (panel != null)
        {
            panel.alpha = 1f;
            panel.blocksRaycasts = false;
            panel.interactable = false;
        }

        yield return new WaitForSeconds(visibleSeconds);

        float t = 0f;
        while (t < fadeOutSeconds && panel != null)
        {
            t += Time.deltaTime;
            panel.alpha = Mathf.Lerp(1f, 0f, t / fadeOutSeconds);
            yield return null;
        }

        HideImmediate();
        activeRoutine = null;
    }

    private void HideImmediate()
    {
        if (panel != null)
        {
            panel.alpha = 0f;
            panel.blocksRaycasts = false;
            panel.interactable = false;
        }
    }
}
