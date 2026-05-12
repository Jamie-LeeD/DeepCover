using UnityEngine;

/// <summary>
/// Example interactable that plays dialogue lines when the player interacts.
/// </summary>
public class NpcInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string npcName = "NPC";
    [SerializeField] private string[] dialogueLines =
    {
        "Hello there.",
        "Stay safe out there."
    };

    private int nextDialogueIndex;

    public string GetInteractionPrompt()
    {
        return $"talk to {npcName}";
    }

    public bool CanInteract(GameObject interactor)
    {
        return dialogueLines != null && dialogueLines.Length > 0;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        string line = dialogueLines[nextDialogueIndex];
        nextDialogueIndex = (nextDialogueIndex + 1) % dialogueLines.Length;
        Debug.Log($"{npcName}: {line}", this);
    }
}
