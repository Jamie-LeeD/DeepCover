using UnityEngine;

/// <summary>
/// Disables player control and unlocks the cursor while dialogue is active.
/// </summary>
public class DialoguePlayerLock : MonoBehaviour
{
    [SerializeField] private FirstPersonController firstPersonController;
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private Rigidbody playerRigidbody;

    private bool dialogueActive;

    private void Awake()
    {
        if (firstPersonController == null)
        {
            firstPersonController = FindFirstObjectByType<FirstPersonController>();
        }

        if (playerInteractor == null)
        {
            playerInteractor = FindFirstObjectByType<PlayerInteractor>();
        }

        if (playerRigidbody == null && firstPersonController != null)
        {
            playerRigidbody = firstPersonController.GetComponent<Rigidbody>();
        }
    }

    public void SetDialogueActive(bool active)
    {
        if (dialogueActive == active)
        {
            return;
        }

        dialogueActive = active;

        if (firstPersonController != null)
        {
            firstPersonController.enabled = !active;
        }

        if (playerInteractor != null)
        {
            playerInteractor.enabled = !active;
        }

        if (active)
        {
            StopPlayerMotion();
            UnlockCursor();
            return;
        }

        LockCursor();
    }

    private void StopPlayerMotion()
    {
        if (playerRigidbody == null)
        {
            return;
        }

        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
