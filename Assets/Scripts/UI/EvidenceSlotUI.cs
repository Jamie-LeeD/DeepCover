using System;
using System.Text.RegularExpressions;
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
    [SerializeField] private TextMeshProUGUI collectedAtText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Button selectButton;

    private EvidenceCollectionRecord boundRecord;
    private Action<EvidenceCollectionRecord> selected;

    private void Awake()
    {
        if (selectButton == null)
        {
            selectButton = GetComponent<Button>();
        }

        if (selectButton != null)
        {
            selectButton.onClick.AddListener(HandleSelected);
        }
    }

    private void OnDestroy()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(HandleSelected);
        }
    }

    public void Bind(EvidenceData evidence)
    {
        Bind(evidence != null ? new EvidenceCollectionRecord(evidence, DateTime.MinValue) : null, null);
    }

    public void Bind(EvidenceCollectionRecord record, Action<EvidenceCollectionRecord> onSelected)
    {
        boundRecord = record;
        selected = onSelected;
        EvidenceData evidence = record?.Evidence;

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
            categoryText.text = FormatCategory(evidence.Category);
        }

        if (collectedAtText != null)
        {
            collectedAtText.text = record.CollectedAt == DateTime.MinValue
                ? string.Empty
                : record.CollectedAtDisplay;
        }

        if (descriptionText != null)
        {
            descriptionText.text = evidence.Description;
        }
    }

    private void HandleSelected()
    {
        selected?.Invoke(boundRecord);
    }

    private static string FormatCategory(EvidenceCategory category)
    {
        return Regex.Replace(category.ToString(), "([a-z])([A-Z])", "$1 $2");
    }

    public void RuntimeSetReferences(
        Image icon,
        TextMeshProUGUI name,
        TextMeshProUGUI category,
        TextMeshProUGUI collectedAt,
        TextMeshProUGUI description,
        Button button)
    {
        iconImage = icon;
        nameText = name;
        categoryText = category;
        collectedAtText = collectedAt;
        descriptionText = description;
        selectButton = button;
    }

#if UNITY_EDITOR
    public void EditorSetReferences(
        Image icon,
        TextMeshProUGUI name,
        TextMeshProUGUI category,
        TextMeshProUGUI collectedAt,
        TextMeshProUGUI description,
        Button button)
    {
        RuntimeSetReferences(icon, name, category, collectedAt, description, button);
    }
#endif
}
