/// <summary>
/// Designer-facing label for how evidence should read in suspicion and dialogue systems.
/// Pair with <see cref="EvidenceData.SuspicionModifier"/> (the numeric gameplay effect).
/// </summary>
public enum EvidenceSuspicionTone
{
    /// <summary>No narrative tone hint; tag derived from modifier sign.</summary>
    Neutral = 0,

    /// <summary>Typically negative modifier — player appears credible or authorized.</summary>
    Credibility = 1,

    /// <summary>Typically positive modifier — classified, dangerous, or exposing.</summary>
    Incriminating = 2
}
