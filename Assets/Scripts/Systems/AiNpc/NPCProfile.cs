using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inspector-authored response for asking one NPC about one evidence item.
/// </summary>
[System.Serializable]
public class NpcEvidenceResponse
{
    [SerializeField] private EvidenceData evidenceItem;
    [SerializeField] [Range(0f, 100f)] private float requiredTrustLevel = 60f;
    [TextArea(2, 6)]
    [SerializeField] private string secretRevealed;
    [TextArea(1, 4)]
    [SerializeField] private string successDialogue;
    [TextArea(1, 4)]
    [SerializeField] private string failureDialogue;
    [SerializeField] private float suspicionPenalty = 10f;
    [SerializeField] private bool hasBeenRevealed;
    [System.NonSerialized] private bool runtimeRevealed;

    public EvidenceData EvidenceItem => evidenceItem;
    public float RequiredTrustLevel => requiredTrustLevel;
    public string SecretRevealed => secretRevealed ?? string.Empty;
    public string SuccessDialogue => successDialogue ?? string.Empty;
    public string FailureDialogue => failureDialogue ?? string.Empty;
    public float SuspicionPenalty => suspicionPenalty;
    public bool HasBeenRevealed => hasBeenRevealed || runtimeRevealed;

    public void MarkRevealed()
    {
        runtimeRevealed = true;
    }
}

/// <summary>
/// Authoring data for an AI-driven NPC: personality, secrets, and speech constraints.
/// </summary>
[CreateAssetMenu(fileName = "NPCProfile", menuName = "DeepCover/NPC Profile")]
public class NPCProfile : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string characterDisplayName = "Operative";
    [SerializeField] private string roleTitle = "Informant";
    [SerializeField] private bool isAccusable = true;
    [Tooltip("Stable id used by the accusation system. Defaults to Display Name when empty.")]
    [SerializeField] private string suspectID;
    [Tooltip("Optional name shown in accusation UI. Defaults to Display Name when empty.")]
    [SerializeField] private string accusationDisplayName;

    [Header("Personality")]
    [TextArea(3, 12)]
    [SerializeField] private string personality = "Cautious, dry humor, loyal to their network.";

    [TextArea(2, 8)]
    [SerializeField] private string voiceGuidelines = "Short clauses. Avoid exposition dumps.";

    [Header("Private knowledge (AI only)")]
    [Tooltip("Facts this NPC knows but must not casually reveal to the player.")]
    [TextArea(4, 16)]
    [SerializeField] private string secretsForAi;

    [Header("Defaults")]
    [SerializeField] [Range(0f, 100f)] private float startingTrust = 40f;

    [TextArea(1, 4)]
    [SerializeField] private string fallbackLineOnAiFailure = "…Not now. Maybe later.";

    [Header("Evidence Responses")]
    [Tooltip("Configure trust-gated secrets this NPC can reveal when asked about specific collected evidence.")]
    [SerializeField] private NpcEvidenceResponse[] evidenceResponses;

    public string CharacterDisplayName => characterDisplayName;
    public string RoleTitle => roleTitle;
    public bool IsAccusable => isAccusable;
    public string SuspectID => string.IsNullOrWhiteSpace(suspectID) ? CharacterDisplayName : suspectID;
    public string AccusationDisplayName => string.IsNullOrWhiteSpace(accusationDisplayName)
        ? CharacterDisplayName
        : accusationDisplayName;
    public string Personality => personality;
    public string VoiceGuidelines => voiceGuidelines;
    public string SecretsForAi => secretsForAi ?? string.Empty;
    public float StartingTrust => startingTrust;
    public string FallbackLineOnAiFailure => fallbackLineOnAiFailure;
    public IReadOnlyList<NpcEvidenceResponse> EvidenceResponses => evidenceResponses;

    public bool TryGetEvidenceResponse(EvidenceData evidence, out NpcEvidenceResponse response)
    {
        response = null;
        if (evidence == null || evidenceResponses == null)
        {
            return false;
        }

        string evidenceId = evidence.EvidenceId;
        for (int i = 0; i < evidenceResponses.Length; i++)
        {
            NpcEvidenceResponse candidate = evidenceResponses[i];
            if (candidate?.EvidenceItem == null)
            {
                continue;
            }

            if (candidate.EvidenceItem == evidence || candidate.EvidenceItem.EvidenceId == evidenceId)
            {
                response = candidate;
                return true;
            }
        }

        return false;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(characterDisplayName))
        {
            characterDisplayName = name;
        }

        if (string.IsNullOrWhiteSpace(suspectID))
        {
            suspectID = characterDisplayName;
        }

        if (string.IsNullOrWhiteSpace(accusationDisplayName))
        {
            accusationDisplayName = characterDisplayName;
        }
    }
}
