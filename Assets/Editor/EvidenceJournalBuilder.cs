using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the evidence journal UI under the existing HUD canvas.
/// </summary>
public static class EvidenceJournalBuilder
{
    private const string JournalName = "EvidenceJournal";
    private const string SpyCanvasName = "SpyGame_MainCanvas";
    private const string ManagersName = "Managers";

    [MenuItem("Tools/Add Evidence Journal To Scene", priority = 104)]
    public static void AddEvidenceJournalToScene()
    {
        AddToScene(null);
    }

    [MenuItem("Tools/Add Evidence Journal To Scene", true)]
    public static bool ValidateAddEvidenceJournalToScene()
    {
        return !Application.isPlaying;
    }

    [MenuItem("GameObject/UI/Add Evidence Journal", false, 12)]
    public static void AddFromGameObjectMenu()
    {
        AddToScene(Selection.activeTransform);
    }

    [MenuItem("GameObject/UI/Add Evidence Journal", true)]
    public static bool ValidateAddFromGameObjectMenu()
    {
        return !Application.isPlaying;
    }

    private static void AddToScene(Transform context)
    {
        if (!TryResolveCanvasParent(context, out Canvas canvas, out Transform parent))
        {
            EditorUtility.DisplayDialog(
                "Evidence Journal",
                $"No existing Canvas found. Use your scene HUD (e.g. '{SpyCanvasName}').",
                "OK");
            return;
        }

        EvidenceInventoryUI existing = canvas.GetComponentInChildren<EvidenceInventoryUI>(true);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "Evidence Journal",
                    $"'{JournalName}' already exists under '{canvas.name}'. Replace it?",
                    "Replace",
                    "Cancel"))
            {
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        EnsureEvidenceInventory();
        EvidenceInventoryUI journal = BuildJournal(parent);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = journal.gameObject;
        EditorGUIUtility.PingObject(journal.gameObject);
        Debug.Log($"[EvidenceJournalBuilder] Added '{JournalName}' under '{canvas.name}'. Press I during play to toggle it.");
    }

    internal static EvidenceInventoryUI BuildJournal(Transform canvasParent)
    {
        GameObject root = new GameObject(JournalName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create Evidence Journal");
        Undo.SetTransformParent(root.transform, canvasParent, "Parent Evidence Journal");
        root.transform.SetAsLastSibling();

        RectTransform rootRt = root.GetComponent<RectTransform>();
        StretchFull(rootRt);

        Image backdrop = Undo.AddComponent<Image>(root);
        backdrop.color = new Color(0.02f, 0.035f, 0.055f, 0.9f);
        backdrop.raycastTarget = true;

        CanvasGroup rootGroup = Undo.AddComponent<CanvasGroup>(root);
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        EvidenceInventoryUI journal = Undo.AddComponent<EvidenceInventoryUI>(root);

        GameObject panel = CreatePanel(root.transform, "Panel", new Vector2(1180f, 720f));
        HorizontalLayoutGroup panelLayout = Undo.AddComponent<HorizontalLayoutGroup>(panel);
        panelLayout.padding = new RectOffset(24, 24, 24, 24);
        panelLayout.spacing = 20f;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = true;

        GameObject listPanel = CreatePanel(panel.transform, "EvidenceListPanel", Vector2.zero);
        LayoutElement listLayout = Undo.AddComponent<LayoutElement>(listPanel);
        listLayout.preferredWidth = 430f;
        listLayout.flexibleWidth = 0f;
        ConfigureVertical(listPanel, 12, 12, 12, 12, 10f);

        TextMeshProUGUI listTitle = CreateLabel(listPanel.transform, "ListTitle", "EVIDENCE JOURNAL", 24f, FontStyles.Bold);
        listTitle.characterSpacing = 2.5f;
        AddLayout(listTitle.gameObject, 42f);

        TextMeshProUGUI hint = CreateLabel(listPanel.transform, "Hint", "Press I to close. Select evidence for details.", 13f, FontStyles.Normal);
        hint.color = new Color(0.62f, 0.76f, 0.8f, 1f);
        AddLayout(hint.gameObject, 28f);

        GameObject scrollGo = DefaultControls.CreateScrollView(SpyGameUiBuilder.CreateUiResourcesPublic());
        scrollGo.name = "EvidenceScrollView";
        Undo.RegisterCreatedObjectUndo(scrollGo, "Create Evidence Scroll View");
        Undo.SetTransformParent(scrollGo.transform, listPanel.transform, "Parent Evidence Scroll View");
        LayoutElement scrollLayout = Undo.AddComponent<LayoutElement>(scrollGo);
        scrollLayout.flexibleHeight = 1f;
        scrollLayout.flexibleWidth = 1f;

        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        Transform content = scroll.content;
        VerticalLayoutGroup contentLayout = Undo.AddComponent<VerticalLayoutGroup>(content.gameObject);
        contentLayout.spacing = 8f;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = Undo.AddComponent<ContentSizeFitter>(content.gameObject);
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI emptyText = CreateLabel(content, "EmptyState", "No evidence collected yet.", 15f, FontStyles.Italic);
        emptyText.color = new Color(0.7f, 0.78f, 0.82f, 1f);
        AddLayout(emptyText.gameObject, 44f);

        EvidenceSlotUI rowTemplate = BuildSlotTemplate(content);
        rowTemplate.gameObject.SetActive(false);

        GameObject detailPanel = CreatePanel(panel.transform, "EvidenceDetailPanel", Vector2.zero);
        LayoutElement detailLayout = Undo.AddComponent<LayoutElement>(detailPanel);
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
        LayoutElement descriptionLayout = Undo.AddComponent<LayoutElement>(description.gameObject);
        descriptionLayout.flexibleHeight = 1f;

        Button closeButton = CreateButton(detailPanel.transform, "CloseButton", "CLOSE JOURNAL");
        AddLayout(closeButton.gameObject, 46f);

        journal.EditorSetReferences(
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

        return journal;
    }

    internal static EvidenceInventory EnsureEvidenceInventory()
    {
        EvidenceInventory existing = Object.FindFirstObjectByType<EvidenceInventory>(FindObjectsInactive.Include);
        if (existing != null)
        {
            return existing;
        }

        GameObject managers = GameObject.Find(ManagersName);
        if (managers == null)
        {
            managers = new GameObject(ManagersName);
            Undo.RegisterCreatedObjectUndo(managers, "Create Managers");
        }

        return Undo.AddComponent<EvidenceInventory>(managers);
    }

    private static EvidenceSlotUI BuildSlotTemplate(Transform parent)
    {
        GameObject row = new GameObject("EvidenceSlotTemplate", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(row, "Create Evidence Slot Template");
        Undo.SetTransformParent(row.transform, parent, "Parent Evidence Slot Template");

        Image bg = Undo.AddComponent<Image>(row);
        bg.color = new Color(0.09f, 0.13f, 0.17f, 0.96f);

        Button button = Undo.AddComponent<Button>(row);
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.09f, 0.13f, 0.17f, 0.96f);
        colors.highlightedColor = new Color(0.16f, 0.24f, 0.3f, 1f);
        colors.pressedColor = new Color(0.06f, 0.1f, 0.13f, 1f);
        button.colors = colors;

        HorizontalLayoutGroup layout = Undo.AddComponent<HorizontalLayoutGroup>(row);
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        LayoutElement rowLayout = Undo.AddComponent<LayoutElement>(row);
        rowLayout.minHeight = 76f;
        rowLayout.preferredHeight = 84f;

        GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(iconGo, "Create Evidence Icon");
        Undo.SetTransformParent(iconGo.transform, row.transform, "Parent Evidence Icon");
        Image icon = Undo.AddComponent<Image>(iconGo);
        icon.color = new Color(0.18f, 0.34f, 0.42f, 1f);
        LayoutElement iconLayout = Undo.AddComponent<LayoutElement>(iconGo);
        iconLayout.minWidth = 48f;
        iconLayout.preferredWidth = 48f;

        GameObject textColumn = new GameObject("TextColumn", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(textColumn, "Create Evidence Text Column");
        Undo.SetTransformParent(textColumn.transform, row.transform, "Parent Evidence Text Column");
        ConfigureVertical(textColumn, 0, 0, 0, 0, 2f);
        LayoutElement textColumnLayout = Undo.AddComponent<LayoutElement>(textColumn);
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

        EvidenceSlotUI slot = Undo.AddComponent<EvidenceSlotUI>(row);
        slot.EditorSetReferences(icon, name, category, collectedAt, description, button);
        return slot;
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 size)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(panel, "Create " + name);
        Undo.SetTransformParent(panel.transform, parent, "Parent " + name);

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        Image image = Undo.AddComponent<Image>(panel);
        image.color = new Color(0.045f, 0.07f, 0.1f, 0.96f);

        Outline outline = Undo.AddComponent<Outline>(panel);
        outline.effectColor = new Color(0.35f, 0.62f, 0.72f, 0.45f);
        outline.effectDistance = new Vector2(1f, -1f);
        return panel;
    }

    private static TextMeshProUGUI CreateLabel(
        Transform parent,
        string name,
        string text,
        float fontSize,
        FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        Undo.SetTransformParent(go.transform, parent, "Parent " + name);

        TextMeshProUGUI tmp = Undo.AddComponent<TextMeshProUGUI>(go);
        SpyGameUiBuilder.ApplyDefaultFontPublic(tmp);
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        GameObject buttonGo = TMP_DefaultControls.CreateButton(CreateTmpResources());
        buttonGo.name = name;
        Undo.RegisterCreatedObjectUndo(buttonGo, "Create " + name);
        Undo.SetTransformParent(buttonGo.transform, parent, "Parent " + name);

        TextMeshProUGUI tmp = buttonGo.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            SpyGameUiBuilder.ApplyDefaultFontPublic(tmp);
            tmp.text = label;
            tmp.fontSize = 14f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.characterSpacing = 1.8f;
            tmp.color = Color.white;
        }

        Button button = buttonGo.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.18f, 0.38f, 0.48f, 1f);
        colors.highlightedColor = new Color(0.28f, 0.52f, 0.65f, 1f);
        colors.pressedColor = new Color(0.12f, 0.28f, 0.36f, 1f);
        button.colors = colors;
        return button;
    }

    private static void ConfigureVertical(
        GameObject target,
        int left,
        int right,
        int top,
        int bottom,
        float spacing)
    {
        VerticalLayoutGroup layout = Undo.AddComponent<VerticalLayoutGroup>(target);
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
        LayoutElement layout = Undo.AddComponent<LayoutElement>(go);
        layout.minHeight = preferredHeight;
        layout.preferredHeight = preferredHeight;
    }

    private static TMP_DefaultControls.Resources CreateTmpResources()
    {
        DefaultControls.Resources resources = SpyGameUiBuilder.CreateUiResourcesPublic();
        return new TMP_DefaultControls.Resources
        {
            standard = resources.standard,
            background = resources.background,
            inputField = resources.inputField,
            knob = resources.knob,
            checkmark = resources.checkmark,
            dropdown = resources.dropdown,
            mask = resources.mask
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

    private static bool TryResolveCanvasParent(Transform context, out Canvas canvas, out Transform parent)
    {
        canvas = null;
        parent = null;

        if (context != null)
        {
            canvas = context.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                parent = canvas.transform;
                return true;
            }
        }

        SpyGameUiRootMarker marker = Object.FindAnyObjectByType<SpyGameUiRootMarker>();
        if (marker != null)
        {
            canvas = marker.GetComponent<Canvas>();
            if (canvas != null)
            {
                parent = canvas.transform;
                return true;
            }
        }

        GameObject named = GameObject.Find(SpyCanvasName);
        if (named != null)
        {
            canvas = named.GetComponent<Canvas>();
            if (canvas != null)
            {
                parent = canvas.transform;
                return true;
            }
        }

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Canvas candidate in canvases)
        {
            if (candidate.renderMode != RenderMode.ScreenSpaceOverlay &&
                candidate.renderMode != RenderMode.ScreenSpaceCamera)
            {
                continue;
            }

            canvas = candidate;
            parent = candidate.transform;
            return true;
        }

        if (canvases.Length > 0)
        {
            canvas = canvases[0];
            parent = canvas.transform;
            return true;
        }

        return false;
    }
}
