using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Global suspicion state, threshold events, and persistence for the spy game.
/// </summary>
public class SuspicionManager : MonoBehaviour, ISuspicionReader, ISuspicionDialogueContext
{
    public const float MinSuspicion = 0f;
    public const float MaxSuspicion = 100f;
    private const string SuspicionSaveKey = "DeepCover.Suspicion.Value";

    public static SuspicionManager Instance { get; private set; }

    [Header("Thresholds")]
    [SerializeField] private float lowThreshold = 25f;
    [SerializeField] private float mediumThreshold = 50f;
    [SerializeField] private float highThreshold = 75f;
    [SerializeField] private float criticalThreshold = 90f;

    [Header("Persistence")]
    [SerializeField] private bool loadSavedStateOnAwake = true;
    [SerializeField] private bool saveStateOnChange = true;

    [Header("Events")]
    [SerializeField] private UnityEvent<float> onSuspicionChanged;
    [SerializeField] private UnityEvent<SuspicionLevel> onSuspicionLevelChanged;

    [Header("Debug")]
    [SerializeField] private float inspectorSuspicion;
    [SerializeField] private SuspicionLevel inspectorLevel;

    private float currentSuspicion;
    private SuspicionLevel currentLevel = SuspicionLevel.Clear;

    public event Action<float> SuspicionChanged;
    public event Action<SuspicionLevel> SuspicionLevelChanged;

    public float SuspicionValue => currentSuspicion;
    public float CurrentSuspicion => currentSuspicion;
    public SuspicionLevel CurrentLevel => currentLevel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (loadSavedStateOnAwake)
        {
            LoadState();
            return;
        }

        ApplySuspicion(currentSuspicion, false);
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
        inspectorSuspicion = currentSuspicion;
        inspectorLevel = currentLevel;
    }

    /// <summary>
    /// Increases global suspicion and clamps the result to the valid range.
    /// </summary>
    public void AddSuspicion(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        ApplySuspicion(currentSuspicion + amount, saveStateOnChange);
    }

    /// <summary>
    /// Decreases global suspicion and clamps the result to the valid range.
    /// </summary>
    public void ReduceSuspicion(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        ApplySuspicion(currentSuspicion - amount, saveStateOnChange);
    }

    /// <summary>
    /// Sets suspicion directly. Useful for debugging, cutscenes, and save loading.
    /// </summary>
    public void SetSuspicion(float value, bool persistState = true)
    {
        ApplySuspicion(value, persistState);
    }

    /// <summary>
    /// Writes the current suspicion value to PlayerPrefs.
    /// </summary>
    public void SaveState()
    {
        PlayerPrefs.SetFloat(SuspicionSaveKey, currentSuspicion);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Restores suspicion from PlayerPrefs.
    /// </summary>
    public void LoadState()
    {
        float savedValue = PlayerPrefs.GetFloat(SuspicionSaveKey, MinSuspicion);
        ApplySuspicion(savedValue, false);
    }

    public float GetSuspicionValue()
    {
        return currentSuspicion;
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
            _ => "suspicion_clear"
        };
    }

    private void ApplySuspicion(float value, bool persistState)
    {
        float clampedValue = Mathf.Clamp(value, MinSuspicion, MaxSuspicion);
        SuspicionLevel newLevel = EvaluateLevel(clampedValue);
        bool valueChanged = !Mathf.Approximately(clampedValue, currentSuspicion);
        bool levelChanged = newLevel != currentLevel;

        currentSuspicion = clampedValue;
        currentLevel = newLevel;

        if (valueChanged)
        {
            SuspicionChanged?.Invoke(currentSuspicion);
            onSuspicionChanged?.Invoke(currentSuspicion);
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

    private SuspicionLevel EvaluateLevel(float suspicionValue)
    {
        if (suspicionValue >= criticalThreshold)
        {
            return SuspicionLevel.Critical;
        }

        if (suspicionValue >= highThreshold)
        {
            return SuspicionLevel.High;
        }

        if (suspicionValue >= mediumThreshold)
        {
            return SuspicionLevel.Medium;
        }

        if (suspicionValue >= lowThreshold)
        {
            return SuspicionLevel.Low;
        }

        return SuspicionLevel.Clear;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        lowThreshold = Mathf.Clamp(lowThreshold, MinSuspicion, MaxSuspicion);
        mediumThreshold = Mathf.Clamp(mediumThreshold, lowThreshold, MaxSuspicion);
        highThreshold = Mathf.Clamp(highThreshold, mediumThreshold, MaxSuspicion);
        criticalThreshold = Mathf.Clamp(criticalThreshold, highThreshold, MaxSuspicion);
        currentSuspicion = Mathf.Clamp(currentSuspicion, MinSuspicion, MaxSuspicion);
        currentLevel = EvaluateLevel(currentSuspicion);
        inspectorSuspicion = currentSuspicion;
        inspectorLevel = currentLevel;
    }
#endif
}
