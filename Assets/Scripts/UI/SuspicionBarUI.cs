using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the global trust/suspicion value as a fixed horizontal scale.
/// </summary>
public class SuspicionBarUI : MonoBehaviour
{
    [Header("Scale")]
    [SerializeField] private RectTransform trackRect;
    [SerializeField] private RectTransform indicatorRect;
    [SerializeField] private RectTransform centerMarkerRect;
    [SerializeField] private float indicatorSmoothTime = 0.08f;

    [Header("Legacy Slider Support")]
    [SerializeField] private Slider suspicionSlider;
    [SerializeField] private Image fillImage;

    [Header("Labels")]
    [SerializeField] private TextMeshProUGUI levelLabel;
    [SerializeField] private TextMeshProUGUI valueLabel;
    [SerializeField] private bool hideWhenClear = false;

    [Header("Colors")]
    [SerializeField] private Color neutralColor = new Color(0.75f, 0.82f, 0.85f);
    [SerializeField] private Color trustColor = new Color(0.2f, 0.8f, 0.45f);
    [SerializeField] private Color lowSuspicionColor = new Color(0.9f, 0.85f, 0.2f);
    [SerializeField] private Color mediumSuspicionColor = new Color(1f, 0.6f, 0.1f);
    [SerializeField] private Color highSuspicionColor = new Color(1f, 0.35f, 0.1f);
    [SerializeField] private Color criticalSuspicionColor = new Color(0.9f, 0.1f, 0.1f);

    private Image indicatorImage;
    private Image centerMarkerImage;
    private float targetNormalized = 0.5f;
    private float displayedNormalized = 0.5f;
    private float indicatorVelocity;
    private bool subscribed;

    private void Awake()
    {
        AutoResolveReferences();
        displayedNormalized = targetNormalized;
        ApplyIndicatorPosition(displayedNormalized);
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (!subscribed || SuspicionManager.Instance == null)
        {
            return;
        }

        SuspicionManager.Instance.SuspicionChanged -= HandleSuspicionChanged;
        SuspicionManager.Instance.SuspicionLevelChanged -= HandleSuspicionLevelChanged;
        subscribed = false;
    }

    private void Update()
    {
        if (!subscribed)
        {
            TrySubscribe();
        }

        if (Mathf.Approximately(displayedNormalized, targetNormalized))
        {
            return;
        }

        displayedNormalized = Mathf.SmoothDamp(
            displayedNormalized,
            targetNormalized,
            ref indicatorVelocity,
            indicatorSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);
        ApplyIndicatorPosition(displayedNormalized);
    }

    private void HandleSuspicionChanged(float suspicionValue)
    {
        if (SuspicionManager.Instance == null)
        {
            return;
        }

        Refresh(suspicionValue, SuspicionManager.Instance.CurrentLevel);
    }

    private void HandleSuspicionLevelChanged(SuspicionLevel level)
    {
        if (SuspicionManager.Instance == null)
        {
            return;
        }

        Refresh(SuspicionManager.Instance.SuspicionValue, level);
    }

    private void Refresh(float suspicionValue, SuspicionLevel level)
    {
        // Value mapping:
        // -100 (maximum suspicion) -> normalized 0.0 -> far left of track
        //    0 (neutral)           -> normalized 0.5 -> center marker
        // +100 (maximum trust)     -> normalized 1.0 -> far right of track
        targetNormalized = Mathf.InverseLerp(
            SuspicionManager.MinReputation,
            SuspicionManager.MaxReputation,
            suspicionValue);

        if (suspicionSlider != null)
        {
            suspicionSlider.minValue = 0f;
            suspicionSlider.maxValue = 1f;
            suspicionSlider.interactable = false;
        }

        if (fillImage != null)
        {
            fillImage.fillAmount = 1f;
            fillImage.color = GetColorForLevel(level);
        }

        if (indicatorImage != null)
        {
            indicatorImage.color = GetColorForLevel(level);
        }

        if (centerMarkerImage != null)
        {
            centerMarkerImage.color = neutralColor;
        }

        if (levelLabel != null)
        {
            levelLabel.text = GetLabelForLevel(level);
        }

        if (valueLabel != null)
        {
            valueLabel.text = suspicionValue.ToString("+0;-0;0");
        }

        if (hideWhenClear)
        {
            bool visible = level != SuspicionLevel.Neutral && level != SuspicionLevel.Clear;
            gameObject.SetActive(visible);
        }

    }

    private Color GetColorForLevel(SuspicionLevel level)
    {
        return level switch
        {
            SuspicionLevel.Trusted => trustColor,
            SuspicionLevel.Low => lowSuspicionColor,
            SuspicionLevel.Medium => mediumSuspicionColor,
            SuspicionLevel.High => highSuspicionColor,
            SuspicionLevel.Critical => criticalSuspicionColor,
            _ => neutralColor
        };
    }

    private string GetLabelForLevel(SuspicionLevel level)
    {
        return level switch
        {
            SuspicionLevel.Trusted => "Trusted",
            SuspicionLevel.Low => "Suspicious",
            SuspicionLevel.Medium => "Watched",
            SuspicionLevel.High => "Compromised",
            SuspicionLevel.Critical => "Critical",
            _ => "Neutral"
        };
    }

    private void TrySubscribe()
    {
        if (subscribed || SuspicionManager.Instance == null)
        {
            return;
        }

        SuspicionManager.Instance.SuspicionChanged += HandleSuspicionChanged;
        SuspicionManager.Instance.SuspicionLevelChanged += HandleSuspicionLevelChanged;
        subscribed = true;
        Refresh(SuspicionManager.Instance.SuspicionValue, SuspicionManager.Instance.CurrentLevel);
    }

    private void ApplyIndicatorPosition(float normalizedPosition)
    {
        if (indicatorRect == null || trackRect == null)
        {
            return;
        }

        float trackWidth = trackRect.rect.width;
        float x = Mathf.Lerp(-trackWidth * 0.5f, trackWidth * 0.5f, Mathf.Clamp01(normalizedPosition));
        Vector2 anchored = indicatorRect.anchoredPosition;
        anchored.x = x;
        indicatorRect.anchoredPosition = anchored;
    }

    private void AutoResolveReferences()
    {
        if (trackRect == null && suspicionSlider != null)
        {
            trackRect = suspicionSlider.GetComponent<RectTransform>();
        }

        if (indicatorRect == null && suspicionSlider != null)
        {
            Transform handle = suspicionSlider.transform.Find("Handle Slide Area/Handle");
            if (handle != null)
            {
                indicatorRect = handle.GetComponent<RectTransform>();
            }
        }

        if (indicatorRect != null)
        {
            indicatorImage = indicatorRect.GetComponent<Image>();
        }

        if (trackRect != null && centerMarkerRect == null)
        {
            float markerHeight = Mathf.Max(28f, trackRect.rect.height + 10f);
            centerMarkerRect = CreateRuntimeMarker("NeutralCenterMarker", new Vector2(3f, markerHeight));
        }

        if (trackRect != null && indicatorRect == null)
        {
            float indicatorHeight = Mathf.Max(34f, trackRect.rect.height + 16f);
            indicatorRect = CreateRuntimeMarker("TrustSuspicionIndicator", new Vector2(12f, indicatorHeight));
        }

        if (centerMarkerRect != null)
        {
            centerMarkerImage = centerMarkerRect.GetComponent<Image>();
            CenterMarkerAtNeutral();
        }

        if (indicatorRect != null)
        {
            indicatorImage = indicatorRect.GetComponent<Image>();
        }
    }

    private void CenterMarkerAtNeutral()
    {
        Vector2 anchored = centerMarkerRect.anchoredPosition;
        anchored.x = 0f;
        centerMarkerRect.anchoredPosition = anchored;
    }

    private RectTransform CreateRuntimeMarker(string markerName, Vector2 size)
    {
        GameObject marker = new GameObject(markerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        marker.transform.SetParent(trackRect, false);

        RectTransform rt = marker.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        Image image = marker.GetComponent<Image>();
        image.color = neutralColor;
        image.raycastTarget = false;
        return rt;
    }

#if UNITY_EDITOR
    public void EditorSetReferences(
        RectTransform track,
        RectTransform indicator,
        RectTransform centerMarker,
        TextMeshProUGUI level,
        TextMeshProUGUI value)
    {
        trackRect = track;
        indicatorRect = indicator;
        centerMarkerRect = centerMarker;
        levelLabel = level;
        valueLabel = value;
        AutoResolveReferences();
    }
#endif
}
