using System;
using UnityEngine;

/// <summary>
/// Read-only access to the global trust/suspicion value and current band.
/// </summary>
public interface ISuspicionReader
{
    /// <summary>-100 = maximum suspicion, 0 = neutral, +100 = maximum trust.</summary>
    float SuspicionValue { get; }
    SuspicionLevel CurrentLevel { get; }
}

/// <summary>
/// Optional hook for future AI dialogue systems that react to suspicion changes.
/// </summary>
public interface ISuspicionDialogueContext
{
    float GetSuspicionValue();
    SuspicionLevel GetSuspicionLevel();
    string GetSuspicionDialogueTag();
}
