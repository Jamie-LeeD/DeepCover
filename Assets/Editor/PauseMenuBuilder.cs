using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds pause menu UI under the existing HUD canvas and wires <see cref="GameplayPauseController"/> on Managers.
/// </summary>
public static class PauseMenuBuilder
{
    private const string PanelName = "PauseMenuView";
    private const string SpyCanvasName = "SpyGame_MainCanvas";
    private const string ManagersName = "Managers";

    [MenuItem("Tools/Add Pause Menu To Scene", priority = 103)]
    public static void AddPauseMenuToScene()
    {
        AddToScene(null);
    }

    [MenuItem("Tools/Add Pause Menu To Scene", true)]
    public static bool ValidateAddPauseMenuToScene()
    {
        return !Application.isPlaying;
    }

    [MenuItem("GameObject/UI/Add Pause Menu", false, 11)]
    public static void AddFromGameObjectMenu()
    {
        AddToScene(Selection.activeTransform);
    }

    [MenuItem("GameObject/UI/Add Pause Menu", true)]
    public static bool ValidateAddFromGameObjectMenu()
    {
        return !Application.isPlaying;
    }

    private static void AddToScene(Transform context)
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Pause Menu", "Exit Play Mode before building UI.", "OK");
            return;
        }

        if (!TryResolveCanvasParent(context, out Canvas canvas, out Transform parent))
        {
            EditorUtility.DisplayDialog(
                "Pause Menu",
                $"No existing Canvas found. Use your scene HUD (e.g. '{SpyCanvasName}').",
                "OK");
            return;
        }

        PauseMenuView existing = canvas.GetComponentInChildren<PauseMenuView>(true);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "Pause Menu",
                    $"'{PanelName}' already exists under '{canvas.name}'. Replace it?",
                    "Replace",
                    "Cancel"))
            {
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        PauseMenuView view = BuildPauseMenu(parent);
        GameplayPauseController controller = EnsurePauseController();
        WireController(controller, view);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = view.gameObject;
        EditorGUIUtility.PingObject(view.gameObject);
        Debug.Log(
            $"[PauseMenuBuilder] Added '{PanelName}' under '{canvas.name}' and wired GameplayPauseController on '{controller.gameObject.name}'.");
    }

    internal static PauseMenuView BuildPauseMenu(Transform canvasParent)
    {
        GameObject root = new GameObject(PanelName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create Pause Menu");
        Undo.SetTransformParent(root.transform, canvasParent, "Parent Pause Menu");
        root.transform.SetAsLastSibling();

        RectTransform rootRt = root.GetComponent<RectTransform>();
        StretchFull(rootRt);

        Image backdrop = Undo.AddComponent<Image>(root);
        backdrop.color = new Color(0.02f, 0.04f, 0.07f, 0.82f);
        backdrop.raycastTarget = true;

        CanvasGroup rootGroup = Undo.AddComponent<CanvasGroup>(root);
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        PauseMenuView view = Undo.AddComponent<PauseMenuView>(root);

        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(panel, "Create Pause Panel");
        Undo.SetTransformParent(panel.transform, root.transform, "Parent Pause Panel");

        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(420f, 280f);
        panelRt.anchoredPosition = Vector2.zero;

        Image panelBg = Undo.AddComponent<Image>(panel);
        panelBg.color = new Color(0.05f, 0.08f, 0.12f, 0.96f);

        Outline outline = Undo.AddComponent<Outline>(panel);
        outline.effectColor = new Color(0.35f, 0.62f, 0.72f, 0.5f);
        outline.effectDistance = new Vector2(2f, -2f);

        VerticalLayoutGroup layout = Undo.AddComponent<VerticalLayoutGroup>(panel);
        layout.padding = new RectOffset(28, 28, 32, 28);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI title = CreateLabel(panel.transform, "TitleLabel", "PAUSED", 28f, FontStyles.Bold);
        title.characterSpacing = 4f;
        title.color = new Color(0.75f, 0.88f, 0.92f, 1f);

        LayoutElement titleLayout = Undo.AddComponent<LayoutElement>(title.gameObject);
        titleLayout.minHeight = 48f;
        titleLayout.preferredHeight = 52f;

        Button resume = CreateMenuButton(panel.transform, "ResumeButton", "RESUME", primary: true);
        Button mainMenu = CreateMenuButton(panel.transform, "MainMenuButton", "MAIN MENU", primary: false);

        view.EditorSetReferences(rootGroup, resume, mainMenu, title);
        return view;
    }

    public static GameplayPauseController EnsurePauseControllerPublic() => EnsurePauseController();

    public static void WireControllerPublic(GameplayPauseController controller, PauseMenuView view) =>
        WireController(controller, view);

    private static GameplayPauseController EnsurePauseController()
    {
        GameplayPauseController existing = Object.FindFirstObjectByType<GameplayPauseController>(FindObjectsInactive.Include);
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

        return Undo.AddComponent<GameplayPauseController>(managers);
    }

    private static void WireController(GameplayPauseController controller, PauseMenuView view)
    {
        FirstPersonController fpc = Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
        PlayerInteractor interactor = Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        Rigidbody rb = fpc != null ? fpc.GetComponent<Rigidbody>() : null;

        SpyGameUiBuilder.BindSerializedPublic(
            controller,
            ("pauseMenuView", view),
            ("firstPersonController", fpc),
            ("playerInteractor", interactor),
            ("playerRigidbody", rb));

        EditorUtility.SetDirty(controller);
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
        Undo.SetTransformParent(go.transform, parent, name + " Parent");

        TextMeshProUGUI tmp = Undo.AddComponent<TextMeshProUGUI>(go);
        SpyGameUiBuilder.ApplyDefaultFontPublic(tmp);
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    private static Button CreateMenuButton(Transform parent, string name, string label, bool primary)
    {
        TMP_DefaultControls.Resources tmpRes = new TMP_DefaultControls.Resources
        {
            standard = SpyGameUiBuilder.CreateUiResourcesPublic().standard,
            background = SpyGameUiBuilder.CreateUiResourcesPublic().background,
            inputField = SpyGameUiBuilder.CreateUiResourcesPublic().inputField,
            knob = SpyGameUiBuilder.CreateUiResourcesPublic().knob,
            checkmark = SpyGameUiBuilder.CreateUiResourcesPublic().checkmark,
            dropdown = SpyGameUiBuilder.CreateUiResourcesPublic().dropdown,
            mask = SpyGameUiBuilder.CreateUiResourcesPublic().mask
        };

        GameObject btnGo = TMP_DefaultControls.CreateButton(tmpRes);
        btnGo.name = name;
        Undo.RegisterCreatedObjectUndo(btnGo, "Create " + name);
        Undo.SetTransformParent(btnGo.transform, parent, name + " Parent");

        LayoutElement le = Undo.AddComponent<LayoutElement>(btnGo);
        le.minHeight = 44f;
        le.preferredHeight = 46f;

        TextMeshProUGUI tmp = btnGo.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            SpyGameUiBuilder.ApplyDefaultFontPublic(tmp);
            tmp.text = label;
            tmp.fontSize = 15f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.characterSpacing = 2f;
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
