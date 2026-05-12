using UnityEngine;

/// <summary>
/// Example interactable that opens and closes a hinged door.
/// </summary>
public class DoorInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform doorPivot;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 4f;
    [SerializeField] private bool startOpen;

    private bool isOpen;
    private Quaternion closedRotation;
    private Quaternion openRotation;

    private void Awake()
    {
        if (doorPivot == null)
        {
            doorPivot = transform;
        }

        closedRotation = doorPivot.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        isOpen = startOpen;
        doorPivot.localRotation = isOpen ? openRotation : closedRotation;
    }

    private void Update()
    {
        Quaternion targetRotation = isOpen ? openRotation : closedRotation;
        doorPivot.localRotation = Quaternion.Slerp(
            doorPivot.localRotation,
            targetRotation,
            openSpeed * Time.deltaTime);
    }

    public string GetInteractionPrompt()
    {
        return isOpen ? "close door" : "open door";
    }

    public bool CanInteract(GameObject interactor)
    {
        return doorPivot != null;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        isOpen = !isOpen;
    }
}
