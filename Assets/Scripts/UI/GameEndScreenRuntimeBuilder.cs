using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Creates a functional end-state overlay at runtime when the scene has not been wired yet.
/// </summary>
public static class GameEndScreenRuntimeBuilder
{
    private const string RootName = "GameEndScreen";

    public static GameEndScreenView CreateOrGet(GameStateManager manager)
    {
        GameEndScreenView existing = Object.FindFirstObjectByType<GameEndScreenView>(FindObjectsInactive.Include);
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

    private static GameEndScreenView Build(Transform canvasParent)
    {
        GameObject root = new GameObject(RootName, typeof(RectTransform));
        root.transform.SetParent(canvasParent, false);
        root.transform.SetAsLastSibling();
        StretchFull(root.GetComponent<RectTransform>());

        Image backdrop = root.AddComponent<Image>();
        backdrop.color = new Color(0.015f, 0.02f, 0.03f, 0.92f);
        backdrop.raycastTarget = true;

        CanvasGroup group = root.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        GameEndScreenView view = root.AddComponent<GameEndScreenView>();

        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(620f, 420f);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.04f, 0.065f, 0.095f, 0.98f);
        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.62f, 0.72f, 0.55f);
        outline.effectDistance = new Vector2(2f, -2f);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(34, 34, 40, 34);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI title = CreateLabel(panel.transform, "Title", "GAME OVER", 34f, FontStyles.Bold);
        title.characterSpacing = 4f;
        AddLayout(title.gameObject, 70f);

        TextMeshProUGUI message = CreateLabel(panel.transform, "Message", "Your cover has been blown.", 18f, FontStyles.Normal);
        message.textWrappingMode = TextWrappingModes.Normal;
        AddLayout(message.gameObject, 130f);

        Button primary = CreateButton(panel.transform, "PrimaryButton", "RETRY", out TextMeshProUGUI primaryLabel);
        AddLayout(primary.gameObject, 52f);

        Button mainMenu = CreateButton(panel.transform, "MainMenuButton", "MAIN MENU", out _);
        AddLayout(mainMenu.gameObject, 52f);

        view.RuntimeSetReferences(group, title, message, primaryLabel, primary, mainMenu);
        return view;
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
        label.characterSpacing = 2f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;

        Button button = buttonGo.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.18f, 0.38f, 0.48f, 1f);
        colors.highlightedColor = new Color(0.28f, 0.52f, 0.65f, 1f);
        colors.pressedColor = new Color(0.12f, 0.28f, 0.36f, 1f);
        button.colors = colors;
        return button;
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

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            return canvas;
        }

        GameObject canvasGo = new GameObject("SpyGame_MainCanvas", typeof(RectTransform));
        canvas = canvasGo.AddComponent<Canvas>();
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
        rt.pivot = new Vector2(0.5f, 0.5f);
    }
}
