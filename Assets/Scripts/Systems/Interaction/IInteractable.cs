using UnityEngine;

/// <summary>
/// Contract for objects the player can focus on and interact with.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Text shown in the interaction prompt while this object is focused.
    /// </summary>
    string GetInteractionPrompt();

    /// <summary>
    /// Whether the given interactor is allowed to use this object right now.
    /// </summary>
    bool CanInteract(GameObject interactor);

    /// <summary>
    /// Performs the interaction for the given interactor.
    /// </summary>
    void Interact(GameObject interactor);
}
