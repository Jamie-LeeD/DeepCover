using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Example interactable that can be picked up once by the player.
/// </summary>
public class PickupItemInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string itemName = "Item";
    [SerializeField] private EvidenceData evidenceToCollect;
    [SerializeField] private bool destroyOnPickup = true;
    [SerializeField] private UnityEvent onPickedUp;

    private bool isPickedUp;

    public string GetInteractionPrompt()
    {
        return $"pick up {itemName}";
    }

    public bool CanInteract(GameObject interactor)
    {
        return !isPickedUp;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        isPickedUp = true;

        if (evidenceToCollect != null && EvidenceInventory.Instance != null)
        {
            EvidenceInventory.Instance.TryAddEvidence(evidenceToCollect);
        }

        onPickedUp?.Invoke();
        Debug.Log($"Picked up {itemName}.", this);

        if (destroyOnPickup)
        {
            Destroy(gameObject);
            return;
        }

        gameObject.SetActive(false);
    }
}
