using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Pickup that registers a ScriptableObject evidence item with the global inventory.
/// </summary>
public class EvidencePickupInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private EvidenceData evidence;
    [SerializeField] private bool destroyOnPickup = true;
    [SerializeField] private bool hideWhenAlreadyCollected = true;
    [SerializeField] private UnityEvent<EvidenceData> onCollected;
    [SerializeField] private UnityEvent<EvidenceData> onAlreadyOwned;

    private bool consumed;

    private void Start()
    {
        if (hideWhenAlreadyCollected &&
            evidence != null &&
            EvidenceInventory.Instance != null &&
            EvidenceInventory.Instance.HasEvidence(evidence))
        {
            consumed = true;
            gameObject.SetActive(false);
        }
    }

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
        if (consumed || evidence == null)
        {
            return false;
        }

        return EvidenceInventory.Instance == null || !EvidenceInventory.Instance.HasEvidence(evidence);
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
