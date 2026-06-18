using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Optional interactable for explicit accusation moments.
/// Add this to an accusation button/object, or a child collider of an NPC, and set Accused Id Or Name.
/// </summary>
public class AccusationTarget : MonoBehaviour, IInteractable
{
    [SerializeField] private string accusedIdOrName = "ARCHIVE";
    [SerializeField] private string prompt = "accuse suspect";
    [SerializeField] private UnityEvent<string> onAccused;

    public string GetInteractionPrompt()
    {
        return string.IsNullOrWhiteSpace(prompt) ? $"accuse {accusedIdOrName}" : prompt;
    }

    public bool CanInteract(GameObject interactor)
    {
        return GameStateManager.Instance == null || !GameStateManager.Instance.IsTerminalState;
    }

    public void Interact(GameObject interactor)
    {
        onAccused?.Invoke(accusedIdOrName);

        if (GameStateManager.Instance == null)
        {
            Debug.LogWarning("[AccusationTarget] No GameStateManager in scene.", this);
            return;
        }

        GameStateManager.Instance.Accuse(accusedIdOrName);
    }
}
