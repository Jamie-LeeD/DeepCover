using UnityEngine;

/// <summary>
/// Authoring asset for a single piece of collectible evidence.
/// </summary>
[CreateAssetMenu(fileName = "Evidence", menuName = "DeepCover/Evidence")]
public class EvidenceData : ScriptableObject
{
    [SerializeField] private string displayName = "Evidence";
    [TextArea(3, 10)]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private EvidenceCategory category = EvidenceCategory.Other;

    [Header("Suspicion")]
    [Tooltip(
        "Applied to global suspicion the first time this evidence is collected. " +
        "Positive values raise suspicion (classified, dangerous, or exposing). " +
        "Negative values lower suspicion (credentials, authorization, alibis). " +
        "Zero means no change.")]
    [SerializeField] private float suspicionModifier;

    [Tooltip(
        "Designer hint for prompts and future NPC reactions. Does not change math — use Suspicion Modifier for that.")]
    [SerializeField] private EvidenceSuspicionTone suspicionTone = EvidenceSuspicionTone.Neutral;

    /// <summary>
    /// Stable id for saves and AI context (defaults to asset name).
    /// </summary>
    [SerializeField] private string evidenceId;

    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public EvidenceCategory Category => category;

    /// <summary>First-collect suspicion delta (see tooltips in inspector).</summary>
    public float SuspicionModifier => suspicionModifier;

    public EvidenceSuspicionTone SuspicionTone => suspicionTone;

    public bool AffectsSuspicion => !Mathf.Approximately(suspicionModifier, 0f);

    /// <summary>Short tag for AI / dialogue context (e.g. credibility, incriminating).</summary>
    public string GetSuspicionContextTag()
    {
        if (!AffectsSuspicion)
        {
            return "neutral";
        }

        if (suspicionTone != EvidenceSuspicionTone.Neutral)
        {
            return suspicionTone.ToString().ToLowerInvariant();
        }

        return suspicionModifier > 0f ? "incriminating" : "credibility";
    }

    public string EvidenceId
    {
        get
        {
            if (string.IsNullOrWhiteSpace(evidenceId))
            {
                return name;
            }

            return evidenceId;
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = name;
        }
    }
}
