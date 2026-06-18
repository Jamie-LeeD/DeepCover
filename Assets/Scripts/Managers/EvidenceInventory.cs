using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Runtime record for one discovered evidence item.
/// </summary>
public sealed class EvidenceCollectionRecord
{
    public EvidenceCollectionRecord(EvidenceData evidence, DateTime collectedAt)
    {
        Evidence = evidence;
        CollectedAt = collectedAt;
    }

    public EvidenceData Evidence { get; }
    public DateTime CollectedAt { get; }
    public string CollectedAtDisplay => CollectedAt.ToString("yyyy-MM-dd HH:mm:ss");
}

/// <summary>
/// Global collection of evidence gathered by the player during an investigation.
/// </summary>
public class EvidenceInventory : MonoBehaviour, IEvidenceContextProvider
{
    public static EvidenceInventory Instance { get; private set; }

    [SerializeField] private UnityEvent<EvidenceData> onEvidenceCollected;
    [SerializeField] private UnityEvent onInventoryChanged;

    private static readonly List<EvidenceCollectionRecord> runtimeCollectedRecords =
        new List<EvidenceCollectionRecord>();
    private static readonly HashSet<string> runtimeCollectedIds = new HashSet<string>();

    private readonly List<EvidenceData> collectedEvidenceView = new List<EvidenceData>();

    public event Action<EvidenceData> EvidenceCollected;
    public event Action InventoryChanged;

    public IReadOnlyList<EvidenceData> CollectedEvidence
    {
        get
        {
            RebuildEvidenceView();
            return collectedEvidenceView;
        }
    }

    public IReadOnlyList<EvidenceCollectionRecord> CollectedRecords => runtimeCollectedRecords;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        runtimeCollectedRecords.Clear();
        runtimeCollectedIds.Clear();
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureEvidenceJournalInput();
        EnsureEvidenceSuspicionBridge();
        EnsureRevealedSecretDatabase();
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

        string evidenceId = GetStableEvidenceId(evidence);
        if (!runtimeCollectedIds.Add(evidenceId))
        {
            return false;
        }

        runtimeCollectedRecords.Add(new EvidenceCollectionRecord(evidence, DateTime.Now));
        Debug.Log(
            $"[Investigation] Evidence Collected: {evidence.DisplayName} ({evidence.EvidenceId}). " +
            $"Total Collected: {runtimeCollectedRecords.Count}",
            evidence);
        EvidenceCollected?.Invoke(evidence);
        onEvidenceCollected?.Invoke(evidence);
        InventoryChanged?.Invoke();
        onInventoryChanged?.Invoke();
        return true;
    }

    public bool HasEvidence(EvidenceData evidence)
    {
        return evidence != null && runtimeCollectedIds.Contains(GetStableEvidenceId(evidence));
    }

    public bool HasEvidenceById(string evidenceId)
    {
        if (string.IsNullOrWhiteSpace(evidenceId))
        {
            return false;
        }

        return runtimeCollectedIds.Contains(evidenceId);
    }

    public EvidenceCollectionRecord GetRecord(EvidenceData evidence)
    {
        if (evidence == null)
        {
            return null;
        }

        string evidenceId = GetStableEvidenceId(evidence);
        for (int i = 0; i < runtimeCollectedRecords.Count; i++)
        {
            EvidenceCollectionRecord record = runtimeCollectedRecords[i];
            if (record?.Evidence != null && GetStableEvidenceId(record.Evidence) == evidenceId)
            {
                return record;
            }
        }

        return null;
    }

    public void ClearRuntimeEvidence()
    {
        runtimeCollectedRecords.Clear();
        runtimeCollectedIds.Clear();
        collectedEvidenceView.Clear();
        InventoryChanged?.Invoke();
        onInventoryChanged?.Invoke();
    }

    public string BuildEvidenceContextSummary()
    {
        if (runtimeCollectedRecords.Count == 0)
        {
            return "No evidence collected.";
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < runtimeCollectedRecords.Count; i++)
        {
            EvidenceData item = runtimeCollectedRecords[i].Evidence;
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

        if (RevealedSecretDatabase.Instance != null &&
            RevealedSecretDatabase.Instance.RevealedSecrets.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("[Revealed Secrets]");
            builder.Append(RevealedSecretDatabase.Instance.BuildSecretContextSummary());
        }

        return builder.ToString();
    }

    private void RebuildEvidenceView()
    {
        collectedEvidenceView.Clear();
        for (int i = 0; i < runtimeCollectedRecords.Count; i++)
        {
            EvidenceData evidence = runtimeCollectedRecords[i]?.Evidence;
            if (evidence != null)
            {
                collectedEvidenceView.Add(evidence);
            }
        }
    }

    private static string GetStableEvidenceId(EvidenceData evidence)
    {
        return evidence != null ? evidence.EvidenceId : string.Empty;
    }

    private void EnsureEvidenceJournalInput()
    {
        EvidenceInventoryUI journal = FindFirstObjectByType<EvidenceInventoryUI>(FindObjectsInactive.Include);
        if (journal != null)
        {
            if (!journal.gameObject.activeSelf)
            {
                journal.gameObject.SetActive(true);
            }

            return;
        }

        if (GetComponent<EvidenceJournalHotkey>() == null)
        {
            gameObject.AddComponent<EvidenceJournalHotkey>();
        }
    }

    private void EnsureEvidenceSuspicionBridge()
    {
        if (GetComponent<EvidenceSuspicionBridge>() == null)
        {
            gameObject.AddComponent<EvidenceSuspicionBridge>();
        }
    }

    private void EnsureRevealedSecretDatabase()
    {
        if (FindFirstObjectByType<RevealedSecretDatabase>(FindObjectsInactive.Include) == null)
        {
            gameObject.AddComponent<RevealedSecretDatabase>();
        }
    }
}
