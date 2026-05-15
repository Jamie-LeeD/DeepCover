using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Pickup that registers a ScriptableObject evidence item with the global inventory.
/// </summary>
public class EvidencePickupInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private EvidenceData evidence;
    [SerializeField] private bool destroyOnPickup = true;
    [SerializeField] private UnityEvent<EvidenceData> onCollected;
    [SerializeField] private UnityEvent<EvidenceData> onAlreadyOwned;

    private bool consumed;

    public string GetInteractionPrompt()
    {
        if (evidence == null)
        {
            return "collect evidence";
        }

        return $"collect {evidence.DisplayName}";
    }

    public bool CanInteract(GameObject interactor)
    {
        return !consumed && evidence != null;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        if (EvidenceInventory.Instance == null)
        {
            Debug.LogError($"{nameof(EvidenceInventory)} is missing from the scene.", this);
            return;
        }

        if (!EvidenceInventory.Instance.TryAddEvidence(evidence))
        {
            onAlreadyOwned?.Invoke(evidence);
            return;
        }

        consumed = true;
        onCollected?.Invoke(evidence);

        if (destroyOnPickup)
        {
            Destroy(gameObject);
            return;
        }

        gameObject.SetActive(false);
    }
}
