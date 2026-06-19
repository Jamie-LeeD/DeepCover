using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds <see cref="DialogueQuestionInputView"/> under the project's existing HUD canvas (no new Canvas / EventSystem).
/// </summary>
public static class DialogueQuestionInputViewBuilder
{
    private const string PanelName = "DialogueQuestionInputView";
    private const string SpyCanvasName = "SpyGame_MainCanvas";

    [MenuItem("Tools/Add Dialogue Question Input To Canvas", priority = 101)]
    public static void AddDialogueQuestionInputToCanvas()
    {
        AddToExistingCanvas(null);
    }

    [MenuItem("GameObject/UI/Add Dialogue Question Input Panel", false, 10)]
    public static void AddFromGameObjectMenu()
    {
        AddToExistingCanvas(Selection.activeTransform);
    }

    [MenuItem("GameObject/UI/Add Dialogue Question Input Panel", true)]
    public static bool ValidateAddFromGameObjectMenu()
    {
        return !Application.isPlaying;
    }

    [MenuItem("Tools/Add Dialogue Question Input To Canvas", true)]
    public static bool ValidateAddDialogueQuestionInputToCanvas()
    {
        return !Application.isPlaying;
    }

    /// <summary>Legacy menu path — forwards to the canvas-only builder.</summary>
    [MenuItem("Tools/Create Dialogue Question Input UI", priority = 102)]
    public static void CreateDialogueQuestionInputUi()
    {
        AddToExistingCanvas(null);
    }

    [MenuItem("Tools/Create Dialogue Question Input UI", true)]
    public static bool ValidateCreateDialogueQuestionInputUi()
    {
        return !Application.isPlaying;
    }

    private static void AddToExistingCanvas(Transform context)
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Dialogue Question Input", "Exit Play Mode before building UI.", "OK");
            return;
        }

        if (!TryResolveCanvasParent(context, out Canvas canvas, out Transform parent))
        {
            EditorUtility.DisplayDialog(
                "Dialogue Question Input",
                $"No existing Canvas found. Use your scene HUD (e.g. '{SpyCanvasName}') — this tool does not create a Canvas or EventSystem.",
                "OK");
            return;
        }

        DialogueQuestionInputView existing = canvas.GetComponentInChildren<DialogueQuestionInputView>(true);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "Dialogue Question Input",
                    $"'{PanelName}' already exists under '{canvas.name}'. Replace it?",
                    "Replace",
                    "Cancel"))
            {
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        DialogueQuestionInputView view = BuildPanel(parent);
        DialogueQuestionInputViewEditor.AutoWire(view);
        TryWireNpcBrainsPublic(view);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = view.gameObject;
        EditorGUIUtility.PingObject(view.gameObject);
        Debug.Log(
            $"[DialogueQuestionInputViewBuilder] Added '{PanelName}' under '{parent.name}' on canvas '{canvas.name}'. " +
            "References wired on DialogueQuestionInputView.");
    }

    internal static DialogueQuestionInputView BuildPanel(Transform canvasParent)
    {
        DefaultControls.Resources res = SpyGameUiBuilder.CreateUiResourcesPublic();

        // Script lives on this root — not on a child.
        GameObject root = new GameObject(PanelName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create Dialogue Question Input");
        Undo.SetTransformParent(root.transform, canvasParent, "Parent Question Input");

        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(720f, 0f);
        rootRt.anchoredPosition = new Vector2(0f, 40f);

        Image backdrop = Undo.AddComponent<Image>(root);
        backdrop.color = new Color(0.03f, 0.05f, 0.08f, 0.94f);
        backdrop.raycastTarget = true;

        Outline outline = Undo.AddComponent<Outline>(root);
        outline.effectColor = new Color(0.35f, 0.62f, 0.72f, 0.45f);
        outline.effectDistance = new Vector2(1f, -1f);

        LayoutElement rootLayout = Undo.AddComponent<LayoutElement>(root);
        rootLayout.minWidth = 520f;
        rootLayout.preferredWidth = 720f;
        rootLayout.flexibleWidth = 1f;

        ContentSizeFitter rootFitter = Undo.AddComponent<ContentSizeFitter>(root);
        rootFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // RootGroup — visibility + layout container
        GameObject rootGroupGo = new GameObject("RootGroup", typeof(RectTransform), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(rootGroupGo, "Create RootGroup");
        Undo.SetTransformParent(rootGroupGo.transform, root.transform, "RootGroup Parent");
        StretchFullScreen(rootGroupGo.GetComponent<RectTransform>());

        CanvasGroup rootGroup = rootGroupGo.GetComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        VerticalLayoutGroup vlg = Undo.AddComponent<VerticalLayoutGroup>(rootGroupGo);
        vlg.padding = new RectOffset(20, 20, 18, 18);
        vlg.spacing = 14f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter groupFitter = Undo.AddComponent<ContentSizeFitter>(rootGroupGo);
        groupFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        groupFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI npcLabel = CreateNpcLabel(rootGroupGo.transform);
        TMP_InputField inputField = CreateQuestionInputField(rootGroupGo.transform, res);
        Button submit = CreateSpyButton(rootGroupGo.transform, res, "SubmitButton", "SUBMIT", true);
        Button cancel = CreateSpyButton(rootGroupGo.transform, res, "CancelButton", "CANCEL", false);

        DialogueQuestionInputView view = Undo.AddComponent<DialogueQuestionInputView>(root);
        SpyGameUiBuilder.BindSerializedPublic(
            view,
            ("rootGroup", rootGroup),
            ("npcLabel", npcLabel),
            ("questionInputField", inputField),
            ("submitButton", submit),
            ("cancelButton", cancel));

        EditorUtility.SetDirty(view);
        return view;
    }

    private static TextMeshProUGUI CreateNpcLabel(Transform parent)
    {
        GameObject go = new GameObject("NPCLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Create NPCLabel");
        Undo.SetTransformParent(go.transform, parent, "NPCLabel Parent");

        LayoutElement le = Undo.AddComponent<LayoutElement>(go);
        le.minHeight = 28f;
        le.preferredHeight = 32f;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        SpyGameUiBuilder.ApplyDefaultFontPublic(tmp);
        tmp.text = "// TARGET";
        tmp.fontSize = 16f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 2f;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = new Color(0.45f, 0.78f, 0.88f, 1f);
        return tmp;
    }

    private static TMP_InputField CreateQuestionInputField(Transform parent, DefaultControls.Resources res)
    {
        TMP_DefaultControls.Resources tmpRes = new TMP_DefaultControls.Resources
        {
            standard = res.standard,
            background = res.background,
            inputField = res.inputField,
            knob = res.knob,
            checkmark = res.checkmark,
            dropdown = res.dropdown,
            mask = res.mask
        };

        GameObject inputRoot = TMP_DefaultControls.CreateInputField(tmpRes);
        inputRoot.name = "QuestionInputField";
        Undo.RegisterCreatedObjectUndo(inputRoot, "Create QuestionInputField");
        Undo.SetTransformParent(inputRoot.transform, parent, "QuestionInputField Parent");

        LayoutElement le = Undo.AddComponent<LayoutElement>(inputRoot);
        le.minHeight = 48f;
        le.preferredHeight = 52f;
        le.flexibleWidth = 1f;

        TMP_InputField field = inputRoot.GetComponent<TMP_InputField>();
        if (field.textComponent is TextMeshProUGUI inputText)
        {
            SpyGameUiBuilder.ApplyDefaultFontPublic(inputText);
            inputText.fontSize = 18f;
            inputText.color = new Color(0.92f, 0.95f, 0.98f, 1f);
        }

        if (field.placeholder is TextMeshProUGUI placeholder)
        {
            SpyGameUiBuilder.ApplyDefaultFontPublic(placeholder);
            placeholder.text = "Type your question…";
            placeholder.fontSize = 16f;
            placeholder.color = new Color(0.55f, 0.62f, 0.68f, 0.85f);
            placeholder.fontStyle = FontStyles.Italic;
        }

        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterLimit = 512;

        Image bg = inputRoot.GetComponent<Image>();
        if (bg != null)
        {
            bg.color = new Color(0.08f, 0.11f, 0.15f, 0.95f);
        }

        return field;
    }

    private static Button CreateSpyButton(Transform parent, DefaultControls.Resources res, string name, string label, bool primary)
    {
        TMP_DefaultControls.Resources tmpRes = new TMP_DefaultControls.Resources
        {
            standard = res.standard,
            background = res.background,
            inputField = res.inputField,
            knob = res.knob,
            checkmark = res.checkmark,
            dropdown = res.dropdown,
            mask = res.mask
        };

        GameObject btnGo = TMP_DefaultControls.CreateButton(tmpRes);
        btnGo.name = name;
        Undo.RegisterCreatedObjectUndo(btnGo, "Create " + name);
        Undo.SetTransformParent(btnGo.transform, parent, name + " Parent");

        LayoutElement le = Undo.AddComponent<LayoutElement>(btnGo);
        le.minHeight = 40f;
        le.preferredHeight = 42f;
        le.minWidth = primary ? 160f : 120f;
        le.preferredWidth = primary ? 180f : 130f;
        le.flexibleWidth = 1f;

        TextMeshProUGUI tmp = btnGo.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            SpyGameUiBuilder.ApplyDefaultFontPublic(tmp);
            tmp.text = label;
            tmp.fontSize = 14f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.characterSpacing = 1.5f;
            tmp.color = Color.white;
        }

        Button button = btnGo.GetComponent<Button>();
        ColorBlock colors = button.colors;
        if (primary)
        {
            colors.normalColor = new Color(0.18f, 0.38f, 0.48f, 1f);
            colors.highlightedColor = new Color(0.28f, 0.52f, 0.65f, 1f);
            colors.pressedColor = new Color(0.12f, 0.28f, 0.36f, 1f);
        }
        else
        {
            colors.normalColor = new Color(0.14f, 0.17f, 0.22f, 1f);
            colors.highlightedColor = new Color(0.22f, 0.26f, 0.32f, 1f);
            colors.pressedColor = new Color(0.1f, 0.12f, 0.16f, 1f);
        }

        button.colors = colors;
        return button;
    }

    /// <summary>
    /// Resolves the spy HUD canvas and the transform to parent under (canvas root, sibling to Dialogue_UI / HUD).
    /// Does not create Canvas or EventSystem.
    /// </summary>
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

    public static void TryWireNpcBrainsPublic(DialogueQuestionInputView view)
    {
        NPCBrain[] brains = Object.FindObjectsByType<NPCBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int wired = 0;
        foreach (NPCBrain brain in brains)
        {
            if (brain == null)
            {
                continue;
            }

            SerializedObject so = new SerializedObject(brain);
            SerializedProperty prop = so.FindProperty("questionInputView");
            if (prop == null)
            {
                continue;
            }

            if (prop.objectReferenceValue != null)
            {
                continue;
            }

            prop.objectReferenceValue = view;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(brain);
            wired++;
        }

        if (wired > 0)
        {
            Debug.Log($"[DialogueQuestionInputViewBuilder] Assigned Question Input View on {wired} NPCBrain(s).");
        }
    }

    private static void StretchFullScreen(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }
}
