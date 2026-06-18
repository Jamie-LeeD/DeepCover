using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Global suspicion state, threshold events, and persistence for the spy game.
/// </summary>
public class SuspicionManager : MonoBehaviour, ISuspicionReader, ISuspicionDialogueContext
{
    public const float MinReputation = -100f;
    public const float NeutralReputation = 0f;
    public const float MaxReputation = 100f;

    // Backward-compatible names for existing UI / gameplay scripts.
    public const float MinSuspicion = MinReputation;
    public const float MaxSuspicion = MaxReputation;
    private const string SuspicionSaveKey = "DeepCover.Suspicion.Value";

    public static SuspicionManager Instance { get; private set; }

    [Header("Thresholds")]
    [Tooltip("Crossing this far left means the player is mildly suspicious.")]
    [SerializeField] private float lowSuspicionThreshold = -25f;
    [SerializeField] private float mediumSuspicionThreshold = -50f;
    [SerializeField] private float highSuspicionThreshold = -75f;
    [SerializeField] private float criticalSuspicionThreshold = -90f;
    [Tooltip("Crossing this far right means the player is considered trusted.")]
    [SerializeField] private float trustedThreshold = 25f;

    [Header("Persistence")]
    [Tooltip("When true, every play session starts at 0 (neutral), ignoring any saved PlayerPrefs value.")]
    [SerializeField] private bool startNeutralOnAwake = true;
    [SerializeField] private bool loadSavedStateOnAwake = true;
    [SerializeField] private bool saveStateOnChange = true;

    [Header("Events")]
    [SerializeField] private UnityEvent<float> onSuspicionChanged;
    [SerializeField] private UnityEvent<SuspicionLevel> onSuspicionLevelChanged;

    [Header("Debug")]
    [SerializeField] private bool logSuspicionDebug;
    [SerializeField] private float inspectorReputation;
    [SerializeField] private SuspicionLevel inspectorLevel;

    private float currentReputation = NeutralReputation;
    private SuspicionLevel currentLevel = SuspicionLevel.Neutral;

    public event Action<float> SuspicionChanged;
    public event Action<SuspicionLevel> SuspicionLevelChanged;

    /// <summary>
    /// Current trust/suspicion value. -100 = maximum suspicion, 0 = neutral, +100 = maximum trust.
    /// </summary>
    public float SuspicionValue => currentReputation;
    public float CurrentSuspicion => currentReputation;
    public float ReputationValue => currentReputation;
    public bool IsAtMaximumSuspicion => currentReputation <= MinReputation + 0.001f;
    public SuspicionLevel CurrentLevel => currentLevel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureGameStateManager();

        if (startNeutralOnAwake)
        {
            ApplyReputation(NeutralReputation, false);
            return;
        }

        if (loadSavedStateOnAwake)
        {
            LoadState();
            return;
        }

        ApplyReputation(currentReputation, false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        inspectorReputation = currentReputation;
        inspectorLevel = currentLevel;
    }

    /// <summary>
    /// Moves reputation toward suspicion (left on the meter).
    /// </summary>
    public void AddSuspicion(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        ApplyReputation(currentReputation - amount, saveStateOnChange);
    }

    /// <summary>
    /// Applies an existing suspicion-style delta. Positive values increase suspicion (left);
    /// negative values reduce suspicion / improve trust (right).
    /// </summary>
    public void ApplySuspicionDelta(float delta, bool persistState = true)
    {
        if (Mathf.Approximately(delta, 0f))
        {
            return;
        }

        ApplyReputation(currentReputation - delta, persistState);
    }

    /// <summary>
    /// Applies a direct reputation delta. Positive values move right toward trust;
    /// negative values move left toward suspicion.
    /// </summary>
    public void ApplyReputationDelta(float delta, bool persistState = true)
    {
        if (Mathf.Approximately(delta, 0f))
        {
            return;
        }

        ApplyReputation(currentReputation + delta, persistState);
    }

    /// <summary>
    /// Moves reputation toward trust (right on the meter).
    /// </summary>
    public void ReduceSuspicion(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        ApplyReputation(currentReputation + amount, saveStateOnChange);
    }

    /// <summary>
    /// Sets the trust/suspicion value directly. -100 = maximum suspicion, 0 = neutral, +100 = maximum trust.
    /// </summary>
    public void SetSuspicion(float value, bool persistState = true)
    {
        ApplyReputation(value, persistState);
    }

    public void SetReputation(float value, bool persistState = true)
    {
        ApplyReputation(value, persistState);
    }

    /// <summary>
    /// Writes the current suspicion value to PlayerPrefs.
    /// </summary>
    public void SaveState()
    {
        PlayerPrefs.SetFloat(SuspicionSaveKey, currentReputation);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Restores suspicion from PlayerPrefs.
    /// </summary>
    public void LoadState()
    {
        float savedValue = PlayerPrefs.GetFloat(SuspicionSaveKey, NeutralReputation);
        ApplyReputation(savedValue, false);
    }

    public float GetSuspicionValue()
    {
        return currentReputation;
    }

    public SuspicionLevel GetSuspicionLevel()
    {
        return currentLevel;
    }

    public string GetSuspicionDialogueTag()
    {
        return currentLevel switch
        {
            SuspicionLevel.Low => "suspicion_low",
            SuspicionLevel.Medium => "suspicion_medium",
            SuspicionLevel.High => "suspicion_high",
            SuspicionLevel.Critical => "suspicion_critical",
            SuspicionLevel.Trusted => "trust_positive",
            _ => "neutral"
        };
    }

    private void ApplyReputation(float value, bool persistState)
    {
        float clampedValue = Mathf.Clamp(value, MinReputation, MaxReputation);
        SuspicionLevel newLevel = EvaluateLevel(clampedValue);
        bool valueChanged = !Mathf.Approximately(clampedValue, currentReputation);
        bool levelChanged = newLevel != currentLevel;

        currentReputation = clampedValue;
        currentLevel = newLevel;

        if (logSuspicionDebug)
        {
            Debug.Log(
                $"[SuspicionManager] value={currentReputation:0.###}, min/max suspicion threshold={MinReputation:0.###}, " +
                $"level={currentLevel}, valueChanged={valueChanged}, levelChanged={levelChanged}",
                this);
        }

        if (valueChanged)
        {
            SuspicionChanged?.Invoke(currentReputation);
            onSuspicionChanged?.Invoke(currentReputation);
        }

        if (levelChanged)
        {
            SuspicionLevelChanged?.Invoke(currentLevel);
            onSuspicionLevelChanged?.Invoke(currentLevel);
        }

        if (persistState && valueChanged)
        {
            SaveState();
        }
    }

    private SuspicionLevel EvaluateLevel(float reputationValue)
    {
        if (reputationValue <= criticalSuspicionThreshold)
        {
            return SuspicionLevel.Critical;
        }

        if (reputationValue <= highSuspicionThreshold)
        {
            return SuspicionLevel.High;
        }

        if (reputationValue <= mediumSuspicionThreshold)
        {
            return SuspicionLevel.Medium;
        }

        if (reputationValue <= lowSuspicionThreshold)
        {
            return SuspicionLevel.Low;
        }

        if (reputationValue >= trustedThreshold)
        {
            return SuspicionLevel.Trusted;
        }

        return SuspicionLevel.Neutral;
    }

    private void EnsureGameStateManager()
    {
        GameStateManager existing = FindFirstObjectByType<GameStateManager>(FindObjectsInactive.Include);
        if (existing == null)
        {
            gameObject.AddComponent<GameStateManager>();
            return;
        }

        if (!existing.gameObject.activeSelf)
        {
            existing.gameObject.SetActive(true);
        }

        if (!existing.enabled)
        {
            existing.enabled = true;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        lowSuspicionThreshold = Mathf.Clamp(lowSuspicionThreshold, MinReputation, NeutralReputation);
        mediumSuspicionThreshold = Mathf.Clamp(mediumSuspicionThreshold, MinReputation, lowSuspicionThreshold);
        highSuspicionThreshold = Mathf.Clamp(highSuspicionThreshold, MinReputation, mediumSuspicionThreshold);
        criticalSuspicionThreshold = Mathf.Clamp(criticalSuspicionThreshold, MinReputation, highSuspicionThreshold);
        trustedThreshold = Mathf.Clamp(trustedThreshold, NeutralReputation, MaxReputation);
        currentReputation = Mathf.Clamp(currentReputation, MinReputation, MaxReputation);
        currentLevel = EvaluateLevel(currentReputation);
        inspectorReputation = currentReputation;
        inspectorLevel = currentLevel;
    }
#endif
}
