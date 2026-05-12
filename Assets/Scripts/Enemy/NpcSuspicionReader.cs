using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Lets NPC logic and future dialogue systems read the current suspicion state.
/// </summary>
public class NpcSuspicionReader : MonoBehaviour, ISuspicionReader
{
    [SerializeField] private UnityEvent<SuspicionLevel> onSuspicionLevelChanged;
    [SerializeField] private UnityEvent<float> onSuspicionValueChanged;

    public float SuspicionValue => SuspicionManager.Instance != null
        ? SuspicionManager.Instance.SuspicionValue
        : 0f;

    public SuspicionLevel CurrentLevel => SuspicionManager.Instance != null
        ? SuspicionManager.Instance.CurrentLevel
        : SuspicionLevel.Clear;

    private void OnEnable()
    {
        if (SuspicionManager.Instance == null)
        {
            return;
        }

        SuspicionManager.Instance.SuspicionChanged += HandleSuspicionChanged;
        SuspicionManager.Instance.SuspicionLevelChanged += HandleSuspicionLevelChanged;
    }

    private void OnDisable()
    {
        if (SuspicionManager.Instance == null)
        {
            return;
        }

        SuspicionManager.Instance.SuspicionChanged -= HandleSuspicionChanged;
        SuspicionManager.Instance.SuspicionLevelChanged -= HandleSuspicionLevelChanged;
    }

    /// <summary>
    /// Returns a dialogue tag that AI dialogue systems can use as a context key.
    /// </summary>
    public string GetDialogueTag()
    {
        if (SuspicionManager.Instance is ISuspicionDialogueContext dialogueContext)
        {
            return dialogueContext.GetSuspicionDialogueTag();
        }

        return "suspicion_clear";
    }

    private void HandleSuspicionChanged(float suspicionValue)
    {
        onSuspicionValueChanged?.Invoke(suspicionValue);
    }

    private void HandleSuspicionLevelChanged(SuspicionLevel level)
    {
        onSuspicionLevelChanged?.Invoke(level);
    }
}
