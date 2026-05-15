using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Populates a scroll list with collected evidence from <see cref="EvidenceInventory"/>.
/// </summary>
public class EvidenceInventoryUI : MonoBehaviour
{
    [SerializeField] private EvidenceSlotUI slotPrefab;
    [SerializeField] private Transform slotContainer;
    [SerializeField] private bool clearContainerOnRefresh = true;

    private void OnEnable()
    {
        if (EvidenceInventory.Instance != null)
        {
            EvidenceInventory.Instance.InventoryChanged += HandleInventoryChanged;
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (EvidenceInventory.Instance != null)
        {
            EvidenceInventory.Instance.InventoryChanged -= HandleInventoryChanged;
        }
    }

    private void HandleInventoryChanged()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (slotPrefab == null || slotContainer == null)
        {
            return;
        }

        if (EvidenceInventory.Instance == null)
        {
            return;
        }

        if (clearContainerOnRefresh)
        {
            for (int i = slotContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(slotContainer.GetChild(i).gameObject);
            }
        }

        IReadOnlyList<EvidenceData> items = EvidenceInventory.Instance.CollectedEvidence;
        for (int i = 0; i < items.Count; i++)
        {
            EvidenceSlotUI slot = Instantiate(slotPrefab, slotContainer);
            slot.Bind(items[i]);
        }
    }
}
