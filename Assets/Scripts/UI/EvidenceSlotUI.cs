using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Single row or tile in the evidence inventory list.
/// </summary>
public class EvidenceSlotUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    public void Bind(EvidenceData evidence)
    {
        if (evidence == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (iconImage != null)
        {
            iconImage.sprite = evidence.Icon;
            iconImage.enabled = evidence.Icon != null;
        }

        if (nameText != null)
        {
            nameText.text = evidence.DisplayName;
        }

        if (categoryText != null)
        {
            categoryText.text = evidence.Category.ToString();
        }

        if (descriptionText != null)
        {
            descriptionText.text = evidence.Description;
        }
    }
}
