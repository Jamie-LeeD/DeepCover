using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the global suspicion value as a UI bar.
/// </summary>
public class SuspicionBarUI : MonoBehaviour
{
    [SerializeField] private Slider suspicionSlider;
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI levelLabel;
    [SerializeField] private bool hideWhenClear = false;

    [Header("Colors")]
    [SerializeField] private Color clearColor = new Color(0.2f, 0.8f, 0.3f);
    [SerializeField] private Color lowColor = new Color(0.9f, 0.85f, 0.2f);
    [SerializeField] private Color mediumColor = new Color(1f, 0.6f, 0.1f);
    [SerializeField] private Color highColor = new Color(1f, 0.35f, 0.1f);
    [SerializeField] private Color criticalColor = new Color(0.9f, 0.1f, 0.1f);

    private void OnEnable()
    {
        if (SuspicionManager.Instance == null)
        {
            return;
        }

        SuspicionManager.Instance.SuspicionChanged += HandleSuspicionChanged;
        SuspicionManager.Instance.SuspicionLevelChanged += HandleSuspicionLevelChanged;
        Refresh(SuspicionManager.Instance.SuspicionValue, SuspicionManager.Instance.CurrentLevel);
    }

    private void OnDisable()
    {
        if (SuspicionManager.Instance == null)
        {
            return;
        }

        SuspicionManager.Instance.SuspicionChanged -= HandleSuspicionChanged;
        SuspicionManager.Instance.SuspicionLevelChanged -= HandleSuspicionLevelChanged;
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
        float normalizedValue = suspicionValue / SuspicionManager.MaxSuspicion;

        if (suspicionSlider != null)
        {
            suspicionSlider.minValue = 0f;
            suspicionSlider.maxValue = 1f;
            suspicionSlider.value = normalizedValue;
        }

        if (fillImage != null)
        {
            fillImage.fillAmount = normalizedValue;
            fillImage.color = GetColorForLevel(level);
        }

        if (levelLabel != null)
        {
            levelLabel.text = level.ToString();
        }

        if (hideWhenClear)
        {
            bool visible = level != SuspicionLevel.Clear;
            gameObject.SetActive(visible);
        }
    }

    private Color GetColorForLevel(SuspicionLevel level)
    {
        return level switch
        {
            SuspicionLevel.Low => lowColor,
            SuspicionLevel.Medium => mediumColor,
            SuspicionLevel.High => highColor,
            SuspicionLevel.Critical => criticalColor,
            _ => clearColor
        };
    }
}
