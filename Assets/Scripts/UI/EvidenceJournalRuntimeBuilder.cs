using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Creates a functional EvidenceInventoryUI at runtime when a scene has not been wired in the editor yet.
/// </summary>
public static class EvidenceJournalRuntimeBuilder
{
    private const string JournalName = "EvidenceJournal";

    public static EvidenceInventoryUI CreateOrGetJournal()
    {
        EvidenceInventoryUI existing = Object.FindFirstObjectByType<EvidenceInventoryUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            return existing;
        }

        Canvas canvas = ResolveCanvas();
        if (canvas == null)
        {
            return null;
        }

        EnsureEventSystem();
        return BuildJournal(canvas.transform);
    }

    private static EvidenceInventoryUI BuildJournal(Transform canvasParent)
    {
        GameObject root = new GameObject(JournalName, typeof(RectTransform));
        root.transform.SetParent(canvasParent, false);
        root.transform.SetAsLastSibling();
        StretchFull(root.GetComponent<RectTransform>());

        Image backdrop = root.AddComponent<Image>();
        backdrop.color = new Color(0.02f, 0.035f, 0.055f, 0.9f);
        backdrop.raycastTarget = true;

        CanvasGroup rootGroup = root.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        EvidenceInventoryUI journal = root.AddComponent<EvidenceInventoryUI>();

        GameObject panel = CreatePanel(root.transform, "Panel", new Vector2(1180f, 720f));
        HorizontalLayoutGroup panelLayout = panel.AddComponent<HorizontalLayoutGroup>();
        panelLayout.padding = new RectOffset(24, 24, 24, 24);
        panelLayout.spacing = 20f;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = true;

        GameObject listPanel = CreatePanel(panel.transform, "EvidenceListPanel", Vector2.zero);
        LayoutElement listLayout = listPanel.AddComponent<LayoutElement>();
        listLayout.preferredWidth = 430f;
        listLayout.flexibleWidth = 0f;
        ConfigureVertical(listPanel, 12, 12, 12, 12, 10f);

        TextMeshProUGUI listTitle = CreateLabel(listPanel.transform, "ListTitle", "EVIDENCE JOURNAL", 24f, FontStyles.Bold);
        listTitle.characterSpacing = 2.5f;
        AddLayout(listTitle.gameObject, 42f);

        TextMeshProUGUI hint = CreateLabel(listPanel.transform, "Hint", "Press I to close. Select evidence for details.", 13f, FontStyles.Normal);
        hint.color = new Color(0.62f, 0.76f, 0.8f, 1f);
        AddLayout(hint.gameObject, 28f);

        GameObject scrollGo = DefaultControls.CreateScrollView(CreateUiResources());
        scrollGo.name = "EvidenceScrollView";
        scrollGo.transform.SetParent(listPanel.transform, false);
        LayoutElement scrollLayout = scrollGo.AddComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1f;
        scrollLayout.flexibleWidth = 1f;

        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        Transform content = scroll.content;
        VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 8f;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI emptyText = CreateLabel(content, "EmptyState", "No evidence collected yet.", 15f, FontStyles.Italic);
        emptyText.color = new Color(0.7f, 0.78f, 0.82f, 1f);
        AddLayout(emptyText.gameObject, 44f);

        EvidenceSlotUI rowTemplate = BuildSlotTemplate(content);
        rowTemplate.gameObject.SetActive(false);

        GameObject detailPanel = CreatePanel(panel.transform, "EvidenceDetailPanel", Vector2.zero);
        LayoutElement detailLayout = detailPanel.AddComponent<LayoutElement>();
        detailLayout.flexibleWidth = 1f;
        ConfigureVertical(detailPanel, 22, 22, 22, 22, 12f);

        TextMeshProUGUI title = CreateLabel(detailPanel.transform, "DetailTitle", "No evidence selected", 26f, FontStyles.Bold);
        title.alignment = TextAlignmentOptions.TopLeft;
        AddLayout(title.gameObject, 52f);

        TextMeshProUGUI category = CreateLabel(detailPanel.transform, "DetailCategory", string.Empty, 15f, FontStyles.Bold);
        category.color = new Color(0.63f, 0.86f, 0.92f, 1f);
        category.alignment = TextAlignmentOptions.TopLeft;
        AddLayout(category.gameObject, 28f);

        TextMeshProUGUI collectedAt = CreateLabel(detailPanel.transform, "DetailCollectedAt", string.Empty, 13f, FontStyles.Italic);
        collectedAt.color = new Color(0.7f, 0.78f, 0.82f, 1f);
        collectedAt.alignment = TextAlignmentOptions.TopLeft;
        AddLayout(collectedAt.gameObject, 26f);

        TextMeshProUGUI description = CreateLabel(
            detailPanel.transform,
            "DetailDescription",
            "Collect evidence from documents, terminals, audio logs, and clues to review it here.",
            16f,
            FontStyles.Normal);
        description.alignment = TextAlignmentOptions.TopLeft;
        description.textWrappingMode = TextWrappingModes.Normal;
        LayoutElement descriptionLayout = description.gameObject.AddComponent<LayoutElement>();
        descriptionLayout.flexibleHeight = 1f;

        Button closeButton = CreateButton(detailPanel.transform, "CloseButton", "CLOSE JOURNAL");
        AddLayout(closeButton.gameObject, 46f);

        journal.RuntimeSetReferences(
            rootGroup,
            rowTemplate,
            content,
            emptyText.gameObject,
            emptyText,
            title,
            category,
            collectedAt,
            description,
            closeButton);

        Debug.Log("[EvidenceJournal] Runtime-created EvidenceJournal UI. Use Tools -> Add Evidence Journal To Scene to save it into the scene.");
        return journal;
    }

    private static EvidenceSlotUI BuildSlotTemplate(Transform parent)
    {
        GameObject row = new GameObject("EvidenceSlotTemplate", typeof(RectTransform));
        row.transform.SetParent(parent, false);

        Image bg = row.AddComponent<Image>();
        bg.color = new Color(0.09f, 0.13f, 0.17f, 0.96f);

        Button button = row.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.09f, 0.13f, 0.17f, 0.96f);
        colors.highlightedColor = new Color(0.16f, 0.24f, 0.3f, 1f);
        colors.pressedColor = new Color(0.06f, 0.1f, 0.13f, 1f);
        button.colors = colors;

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.minHeight = 76f;
        rowLayout.preferredHeight = 84f;

        GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(row.transform, false);
        Image icon = iconGo.AddComponent<Image>();
        icon.color = new Color(0.18f, 0.34f, 0.42f, 1f);
        LayoutElement iconLayout = iconGo.AddComponent<LayoutElement>();
        iconLayout.minWidth = 48f;
        iconLayout.preferredWidth = 48f;

        GameObject textColumn = new GameObject("TextColumn", typeof(RectTransform));
        textColumn.transform.SetParent(row.transform, false);
        ConfigureVertical(textColumn, 0, 0, 0, 0, 2f);
        LayoutElement textColumnLayout = textColumn.AddComponent<LayoutElement>();
        textColumnLayout.flexibleWidth = 1f;

        TextMeshProUGUI name = CreateLabel(textColumn.transform, "Name", "Evidence Title", 15f, FontStyles.Bold);
        name.alignment = TextAlignmentOptions.Left;
        TextMeshProUGUI category = CreateLabel(textColumn.transform, "Category", "Category", 12f, FontStyles.Bold);
        category.color = new Color(0.63f, 0.86f, 0.92f, 1f);
        category.alignment = TextAlignmentOptions.Left;
        TextMeshProUGUI collectedAt = CreateLabel(textColumn.transform, "CollectedAt", "Collected At", 11f, FontStyles.Italic);
        collectedAt.color = new Color(0.72f, 0.78f, 0.82f, 1f);
        collectedAt.alignment = TextAlignmentOptions.Left;
        TextMeshProUGUI description = CreateLabel(textColumn.transform, "Description", string.Empty, 11f, FontStyles.Normal);
        description.gameObject.SetActive(false);

        EvidenceSlotUI slot = row.AddComponent<EvidenceSlotUI>();
        slot.RuntimeSetReferences(icon, name, category, collectedAt, description, button);
        return slot;
    }

    private static Canvas ResolveCanvas()
    {
        SpyGameUiRootMarker marker = Object.FindAnyObjectByType<SpyGameUiRootMarker>();
        if (marker != null && marker.TryGetComponent(out Canvas markedCanvas))
        {
            return markedCanvas;
        }

        GameObject named = GameObject.Find("SpyGame_MainCanvas");
        if (named != null && named.TryGetComponent(out Canvas namedCanvas))
        {
            return namedCanvas;
        }

        Canvas existing = Object.FindFirstObjectByType<Canvas>();
        if (existing != null)
        {
            return existing;
        }

        GameObject canvasGo = new GameObject("SpyGame_MainCanvas", typeof(RectTransform));
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 size)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;

        Image image = panel.AddComponent<Image>();
        image.color = new Color(0.045f, 0.07f, 0.1f, 0.96f);

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.62f, 0.72f, 0.45f);
        outline.effectDistance = new Vector2(1f, -1f);
        return panel;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float fontSize, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        GameObject buttonGo = DefaultControls.CreateButton(CreateUiResources());
        buttonGo.name = name;
        buttonGo.transform.SetParent(parent, false);

        TextMeshProUGUI labelText = buttonGo.GetComponentInChildren<TextMeshProUGUI>();
        Text text = buttonGo.GetComponentInChildren<Text>();
        if (text != null)
        {
            Object.Destroy(text);
        }

        if (labelText == null)
        {
            GameObject labelGo = new GameObject("Text", typeof(RectTransform));
            labelGo.transform.SetParent(buttonGo.transform, false);
            StretchFull(labelGo.GetComponent<RectTransform>());
            labelText = labelGo.AddComponent<TextMeshProUGUI>();
        }

        labelText.font = TMP_Settings.defaultFontAsset;
        labelText.text = label;
        labelText.fontSize = 14f;
        labelText.fontStyle = FontStyles.Bold;
        labelText.characterSpacing = 1.8f;
        labelText.alignment = TextAlignmentOptions.Center;

        Button button = buttonGo.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.18f, 0.38f, 0.48f, 1f);
        colors.highlightedColor = new Color(0.28f, 0.52f, 0.65f, 1f);
        colors.pressedColor = new Color(0.12f, 0.28f, 0.36f, 1f);
        button.colors = colors;
        return button;
    }

    private static void ConfigureVertical(GameObject target, int left, int right, int top, int bottom, float spacing)
    {
        VerticalLayoutGroup layout = target.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(left, right, top, bottom);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private static void AddLayout(GameObject go, float preferredHeight)
    {
        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.minHeight = preferredHeight;
        layout.preferredHeight = preferredHeight;
    }

    private static DefaultControls.Resources CreateUiResources()
    {
        Sprite fallback = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        return new DefaultControls.Resources
        {
            standard = fallback,
            background = fallback,
            inputField = fallback,
            knob = fallback,
            checkmark = fallback,
            dropdown = fallback,
            mask = fallback
        };
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }
}
