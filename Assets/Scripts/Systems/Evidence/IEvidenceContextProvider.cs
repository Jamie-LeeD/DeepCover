using System.Collections.Generic;

/// <summary>
/// Read-only access to collected evidence for UI, dialogue, and future AI prompt building.
/// </summary>
public interface IEvidenceInventoryReader
{
    IReadOnlyList<EvidenceData> CollectedEvidence { get; }
    bool HasEvidence(EvidenceData evidence);
    bool HasEvidenceById(string evidenceId);
}

/// <summary>
/// Optional extension for systems that need a serialized context string (e.g. LLM prompts).
/// </summary>
public interface IEvidenceContextProvider : IEvidenceInventoryReader
{
    /// <summary>
    /// Human-readable summary of owned evidence for AI or design-time debugging.
    /// </summary>
    string BuildEvidenceContextSummary();
}
