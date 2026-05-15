/// <summary>
/// Snapshot of live game values passed into the NPC Ollama prompt builder.
/// </summary>
public readonly struct NpcDialoguePromptContext
{
    public string CharacterName { get; }
    public float TrustLevel { get; }
    public float SuspicionValue { get; }
    public SuspicionLevel SuspicionLevel { get; }
    public string SuspicionDialogueTag { get; }
    public string EvidenceSummary { get; }
    public string MemoryTranscript { get; }
    public string PlayerUtterance { get; }

    public NpcDialoguePromptContext(
        string characterName,
        float trustLevel,
        float suspicionValue,
        SuspicionLevel suspicionLevel,
        string suspicionDialogueTag,
        string evidenceSummary,
        string memoryTranscript,
        string playerUtterance)
    {
        CharacterName = characterName ?? "NPC";
        TrustLevel = trustLevel;
        SuspicionValue = suspicionValue;
        SuspicionLevel = suspicionLevel;
        SuspicionDialogueTag = suspicionDialogueTag ?? string.Empty;
        EvidenceSummary = evidenceSummary ?? string.Empty;
        MemoryTranscript = memoryTranscript ?? string.Empty;
        PlayerUtterance = playerUtterance ?? string.Empty;
    }
}
