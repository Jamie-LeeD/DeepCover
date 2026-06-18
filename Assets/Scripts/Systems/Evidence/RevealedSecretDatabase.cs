using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Runtime database of secrets revealed through trust-gated evidence inquiries.
/// </summary>
public class RevealedSecretDatabase : MonoBehaviour
{
    public static RevealedSecretDatabase Instance { get; private set; }

    private static readonly List<RevealedSecretRecord> revealedSecrets = new List<RevealedSecretRecord>();
    private static readonly HashSet<string> revealedSecretIds = new HashSet<string>();

    public event Action<RevealedSecretRecord> SecretRevealed;

    public IReadOnlyList<RevealedSecretRecord> RevealedSecrets => revealedSecrets;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        revealedSecrets.Clear();
        revealedSecretIds.Clear();
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
    }

    public bool TryRevealSecret(string npcName, EvidenceData evidence, string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        string secretId = BuildSecretId(npcName, evidence, secret);
        if (!revealedSecretIds.Add(secretId))
        {
            return false;
        }

        RevealedSecretRecord record = new RevealedSecretRecord(
            secretId,
            string.IsNullOrWhiteSpace(npcName) ? "Unknown" : npcName,
            evidence,
            secret.Trim(),
            DateTime.Now);

        revealedSecrets.Add(record);
        SecretRevealed?.Invoke(record);
        return true;
    }

    public bool HasSecret(string npcName, EvidenceData evidence, string secret)
    {
        return revealedSecretIds.Contains(BuildSecretId(npcName, evidence, secret));
    }

    public string BuildSecretContextSummary()
    {
        if (revealedSecrets.Count == 0)
        {
            return "No secrets revealed.";
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < revealedSecrets.Count; i++)
        {
            RevealedSecretRecord record = revealedSecrets[i];
            builder.Append("- ");
            builder.Append(record.NpcName);
            if (record.Evidence != null)
            {
                builder.Append(" about ");
                builder.Append(record.Evidence.DisplayName);
            }

            builder.Append(": ");
            builder.AppendLine(record.Secret);
        }

        return builder.ToString();
    }

    private static string BuildSecretId(string npcName, EvidenceData evidence, string secret)
    {
        string evidenceId = evidence != null ? evidence.EvidenceId : "no_evidence";
        string normalizedSecret = string.IsNullOrWhiteSpace(secret) ? string.Empty : secret.Trim().ToLowerInvariant();
        string normalizedNpc = string.IsNullOrWhiteSpace(npcName) ? "unknown" : npcName.Trim().ToLowerInvariant();
        return $"{normalizedNpc}|{evidenceId}|{normalizedSecret}";
    }
}

public sealed class RevealedSecretRecord
{
    public RevealedSecretRecord(string secretId, string npcName, EvidenceData evidence, string secret, DateTime revealedAt)
    {
        SecretId = secretId;
        NpcName = npcName;
        Evidence = evidence;
        Secret = secret;
        RevealedAt = revealedAt;
    }

    public string SecretId { get; }
    public string NpcName { get; }
    public EvidenceData Evidence { get; }
    public string Secret { get; }
    public DateTime RevealedAt { get; }
    public string RevealedAtDisplay => RevealedAt.ToString("yyyy-MM-dd HH:mm:ss");
}
