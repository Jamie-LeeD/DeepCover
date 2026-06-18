using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Runtime fallback for scenes that have not been configured with Tools -> Add Pause Menu To Scene.
/// Reuses an existing canvas when possible and creates only one PauseMenuView.
/// </summary>
public static class PauseMenuRuntimeBuilder
{
    private const string RootName = "PauseMenuView";

    public static PauseMenuView CreateOrGet()
    {
        PauseMenuView existing = Object.FindFirstObjectByType<PauseMenuView>(FindObjectsInactive.Include);
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
        return Build(canvas.transform);
    }

    private static PauseMenuView Build(Transform canvasParent)
    {
        GameObject root = new GameObject(RootName, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        root.transform.SetParent(canvasParent, false);
        root.transform.SetAsLastSibling();
        StretchFull(root.GetComponent<RectTransform>());

        Image backdrop = root.GetComponent<Image>();
        backdrop.color = new Color(0.02f, 0.04f, 0.07f, 0.82f);
        backdrop.raycastTarget = true;

        CanvasGroup rootGroup = root.GetComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(root.transform, false);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(420f, 280f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.05f, 0.08f, 0.12f, 0.96f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 32, 28);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI title = CreateLabel(panel.transform, "TitleLabel", "PAUSED", 28f, FontStyles.Bold);
        title.characterSpacing = 4f;
        AddLayout(title.gameObject, 52f);

        Button resume = CreateButton(panel.transform, "ResumeButton", "RESUME", out _);
        AddLayout(resume.gameObject, 54f);

        Button mainMenu = CreateButton(panel.transform, "MainMenuButton", "MAIN MENU", out _);
        AddLayout(mainMenu.gameObject, 54f);

        PauseMenuView view = root.AddComponent<PauseMenuView>();
        view.RuntimeSetReferences(rootGroup, resume, mainMenu, title);
        return view;
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

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float fontSize, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = new Color(0.75f, 0.88f, 0.92f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    private static Button CreateButton(Transform parent, string name, string text, out TextMeshProUGUI label)
    {
        GameObject buttonGo = DefaultControls.CreateButton(CreateUiResources());
        buttonGo.name = name;
        buttonGo.transform.SetParent(parent, false);

        Text legacyText = buttonGo.GetComponentInChildren<Text>();
        if (legacyText != null)
        {
            Object.Destroy(legacyText);
        }

        GameObject labelGo = new GameObject("Text", typeof(RectTransform));
        labelGo.transform.SetParent(buttonGo.transform, false);
        StretchFull(labelGo.GetComponent<RectTransform>());

        label = labelGo.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = 16f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;

        return buttonGo.GetComponent<Button>();
    }

    private static void AddLayout(GameObject go, float preferredHeight)
    {
        LayoutElement element = go.AddComponent<LayoutElement>();
        element.minHeight = preferredHeight;
        element.preferredHeight = preferredHeight;
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
    }
}
