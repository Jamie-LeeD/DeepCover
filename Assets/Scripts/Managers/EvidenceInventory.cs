using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Global collection of evidence gathered by the player during an investigation.
/// </summary>
public class EvidenceInventory : MonoBehaviour, IEvidenceContextProvider
{
    public static EvidenceInventory Instance { get; private set; }

    [SerializeField] private UnityEvent<EvidenceData> onEvidenceCollected;
    [SerializeField] private UnityEvent onInventoryChanged;

    private readonly List<EvidenceData> collected = new List<EvidenceData>();
    private readonly HashSet<EvidenceData> collectedSet = new HashSet<EvidenceData>();

    public event Action<EvidenceData> EvidenceCollected;
    public event Action InventoryChanged;

    public IReadOnlyList<EvidenceData> CollectedEvidence => collected;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Adds evidence if not already owned. Returns true when newly collected.
    /// </summary>
    public bool TryAddEvidence(EvidenceData evidence)
    {
        if (evidence == null)
        {
            return false;
        }

        if (!collectedSet.Add(evidence))
        {
            return false;
        }

        collected.Add(evidence);
        EvidenceCollected?.Invoke(evidence);
        onEvidenceCollected?.Invoke(evidence);
        InventoryChanged?.Invoke();
        onInventoryChanged?.Invoke();
        return true;
    }

    public bool HasEvidence(EvidenceData evidence)
    {
        return evidence != null && collectedSet.Contains(evidence);
    }

    public bool HasEvidenceById(string evidenceId)
    {
        if (string.IsNullOrWhiteSpace(evidenceId))
        {
            return false;
        }

        for (int i = 0; i < collected.Count; i++)
        {
            if (collected[i] != null && string.Equals(collected[i].EvidenceId, evidenceId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public string BuildEvidenceContextSummary()
    {
        if (collected.Count == 0)
        {
            return "No evidence collected.";
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < collected.Count; i++)
        {
            EvidenceData item = collected[i];
            if (item == null)
            {
                continue;
            }

            builder.Append('[');
            builder.Append(item.EvidenceId);
            builder.Append("] ");
            builder.Append(item.DisplayName);
            builder.Append(" (");
            builder.Append(item.Category);
            builder.Append("; suspicion_tone=");
            builder.Append(item.GetSuspicionContextTag());
            if (item.AffectsSuspicion)
            {
                builder.Append("; collect_delta=");
                builder.Append(item.SuspicionModifier.ToString("+0.#;-0.#;0"));
            }

            builder.Append("): ");
            builder.AppendLine(item.Description);
        }

        return builder.ToString();
    }
}
