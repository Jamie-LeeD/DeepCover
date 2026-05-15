using TMPro;
using UnityEngine;

/// <summary>
/// Optional chrome around the suspicion bar (title + forwards to <see cref="SuspicionBarUI"/>).
/// </summary>
public class SuspicionUIController : MonoBehaviour
{
    [SerializeField] private SuspicionBarUI suspicionBar;
    [SerializeField] private TextMeshProUGUI headerLabel;

    private void Reset()
    {
        if (headerLabel != null)
        {
            headerLabel.text = "SUSPICION";
        }
    }

    private void Awake()
    {
        if (headerLabel != null && string.IsNullOrWhiteSpace(headerLabel.text))
        {
            headerLabel.text = "SUSPICION";
        }
    }

    public SuspicionBarUI SuspicionBar => suspicionBar;
}
