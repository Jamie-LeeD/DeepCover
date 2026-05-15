using UnityEngine;

/// <summary>
/// Authoring data for an AI-driven NPC: personality, secrets, and speech constraints.
/// </summary>
[CreateAssetMenu(fileName = "NPCProfile", menuName = "DeepCover/NPC Profile")]
public class NPCProfile : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string characterDisplayName = "Operative";
    [SerializeField] private string roleTitle = "Informant";

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

    public string CharacterDisplayName => characterDisplayName;
    public string RoleTitle => roleTitle;
    public string Personality => personality;
    public string VoiceGuidelines => voiceGuidelines;
    public string SecretsForAi => secretsForAi ?? string.Empty;
    public float StartingTrust => startingTrust;
    public string FallbackLineOnAiFailure => fallbackLineOnAiFailure;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(characterDisplayName))
        {
            characterDisplayName = name;
        }
    }
}
