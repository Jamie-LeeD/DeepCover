using UnityEngine;

/// <summary>
/// Listens for <see cref="EvidenceInventory.EvidenceCollected"/> and applies each item's
/// <see cref="EvidenceData.SuspicionModifier"/> once (inventory already prevents duplicates).
/// </summary>
/// <remarks>
/// <b>Setup:</b> Add to the same GameObject as <see cref="EvidenceInventory"/> (e.g. Managers → EvidenceInventory).
/// Requires <see cref="SuspicionManager"/> in the scene.
/// </remarks>
[DisallowMultipleComponent]
public class EvidenceSuspicionBridge : MonoBehaviour
{
    [SerializeField] private bool logSuspicionChanges = true;

    private void OnEnable()
    {
        if (EvidenceInventory.Instance != null)
        {
            EvidenceInventory.Instance.EvidenceCollected += HandleEvidenceCollected;
        }
    }

    private void Start()
    {
        if (EvidenceInventory.Instance != null)
        {
            EvidenceInventory.Instance.EvidenceCollected -= HandleEvidenceCollected;
            EvidenceInventory.Instance.EvidenceCollected += HandleEvidenceCollected;
        }
    }

    private void OnDisable()
    {
        if (EvidenceInventory.Instance != null)
        {
            EvidenceInventory.Instance.EvidenceCollected -= HandleEvidenceCollected;
        }
    }

    private void HandleEvidenceCollected(EvidenceData evidence)
    {
        if (evidence == null || !evidence.AffectsSuspicion)
        {
            return;
        }

        SuspicionManager suspicion = SuspicionManager.Instance;
        if (suspicion == null)
        {
            Debug.LogWarning(
                "[EvidenceSuspicion] Evidence collected but SuspicionManager is missing from the scene.",
                this);
            return;
        }

        float before = suspicion.SuspicionValue;
        suspicion.ApplySuspicionDelta(evidence.SuspicionModifier);
        float after = suspicion.SuspicionValue;

        if (!logSuspicionChanges)
        {
            return;
        }

        string direction = evidence.SuspicionModifier > 0f
            ? "moved toward suspicion"
            : "moved toward trust";
        Debug.Log(
            $"[EvidenceSuspicion] Collected '{evidence.DisplayName}' ({evidence.EvidenceId}) — " +
            $"reputation {direction} by {Mathf.Abs(evidence.SuspicionModifier):0.#} " +
            $"({before:0.#} → {after:0.#}, band {suspicion.CurrentLevel}, tone {evidence.GetSuspicionContextTag()}).",
            evidence);
    }
}
