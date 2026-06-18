using UnityEngine;

/// <summary>
/// Runtime fallback for scenes that have an EvidenceInventory but no journal UI yet.
/// Once it creates the journal, EvidenceInventoryUI handles future I-key toggles.
/// </summary>
public class EvidenceJournalHotkey : MonoBehaviour
{
    private void Update()
    {
        if (EvidenceInventoryUI.ShouldIgnoreJournalToggleInput())
        {
            return;
        }

        if (!EvidenceInventoryUI.WasJournalTogglePressed())
        {
            return;
        }

        EvidenceInventoryUI journal = FindFirstObjectByType<EvidenceInventoryUI>(FindObjectsInactive.Include);
        if (journal == null)
        {
            journal = EvidenceJournalRuntimeBuilder.CreateOrGetJournal();
        }

        if (journal == null)
        {
            Debug.LogWarning("[EvidenceJournal] Pressed I, but no Canvas was found for the journal UI.", this);
            return;
        }

        if (!journal.gameObject.activeSelf)
        {
            journal.gameObject.SetActive(true);
        }

        journal.OpenJournal();
        Destroy(this);
    }
}
