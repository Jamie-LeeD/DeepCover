using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// One-click builder for a minimalist spy-investigation HUD + dialogue stack (TMP, anchors, scaler).
/// Safe to re-run: detects existing Spy Game root and optional EventSystem.
/// </summary>
public static class SpyGameUiBuilder
{
    private const string RootName = "SpyGame_MainCanvas";
    private const string PrefabPath = "Assets/Prefabs/UI/SpyGame_MainCanvas.prefab";

    [MenuItem("Tools/Create Spy Game UI", priority = 100)]
    public static void CreateSpyGameUi()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Spy Game UI", "Exit Play Mode before building UI.", "OK");
            return;
        }

        if (UnityEngine.Object.FindAnyObjectByType<SpyGameUiRootMarker>() != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "Spy Game UI",
                    "Spy Game UI already exists in the scene (SpyGameUiRootMarker found). Replace it?",
                    "Replace",
                    "Cancel"))
            {
                return;
            }

            DestroyExistingSpyUiRoot();
        }

        EnsureEventSystemPublic();
        GameObject canvasRoot = CreateCanvasRoot();
        Transform canvasTransform = canvasRoot.transform;

        GameObject hudRoot = CreateHudRoot(canvasTransform);
        Image crosshair = CreateCrosshair(hudRoot.transform);
        TextMeshProUGUI objective = CreateObjectiveText(hudRoot.transform);
        CreateSuspicionMeter(hudRoot.transform, out SuspicionBarUI suspicionBar, out SuspicionUIController suspicionController, out TextMeshProUGUI suspicionHeader);
        CreateInteractionPrompt(hudRoot.transform, out InteractionPromptUI promptUi);
        CreateEvidenceNotification(hudRoot.transform, out EvidenceNotificationUI evidenceNotification);

        CreateDialogueStack(canvasTransform, out DialogueUIView dialogueView, out DialogueUIController dialogueController, out Button evidenceJournalButton);
        DialogueQuestionInputView questionInputView = DialogueQuestionInputViewBuilder.BuildPanel(canvasTransform);

        HUDManager hudManager = canvasRoot.GetComponent<HUDManager>();
        BindSerializedPublic(
            hudManager,
            ("crosshairImage", crosshair),
            ("objectiveText", objective),
            ("interactionPrompt", promptUi),
            ("suspicionUi", suspicionController),
            ("evidenceNotification", evidenceNotification));

        BindSerializedPublic(
            suspicionController,
            ("suspicionBar", suspicionBar),
            ("headerLabel", suspicionHeader));

        BindSerializedPublic(
            dialogueController,
            ("dialogueView", dialogueView),
            ("evidenceJournalButton", evidenceJournalButton));

        WireManagersInScene(dialogueView);
        TryWirePlayerInteractor(promptUi);
        DialogueQuestionInputViewBuilder.TryWireNpcBrainsPublic(questionInputView);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        bool savePrefab = EditorUtility.DisplayDialog(
            "Spy Game UI",
            "UI created under '" + RootName + "'. Save as prefab at " + PrefabPath + "?",
            "Save Prefab",
            "Skip");

        if (savePrefab)
        {
            SavePrefab(canvasRoot);
        }

        Selection.activeGameObject = canvasRoot;
        EditorGUIUtility.PingObject(canvasRoot);
    }

    [MenuItem("Tools/Create Spy Game UI", true)]
    public static bool ValidateCreateSpyGameUi()
    {
        return !Application.isPlaying;
    }

    private static void DestroyExistingSpyUiRoot()
    {
        SpyGameUiRootMarker[] markers = UnityEngine.Object.FindObjectsByType<SpyGameUiRootMarker>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (SpyGameUiRootMarker marker in markers)
        {
            if (marker == null || marker.gameObject == null)
            {
                continue;
            }

            if (!marker.gameObject.scene.IsValid())
            {
                continue;
            }

            Undo.DestroyObjectImmediate(marker.gameObject);
        }
    }

    /// <summary>
    /// Reuses an existing EventSystem when present; otherwise creates one with the Input System UI module.
    /// </summary>
    public static void EnsureEventSystemPublic()
    {
        EventSystem existing = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
        if (existing != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    private static GameObject CreateCanvasRoot()
    {
        GameObject root = new GameObject(RootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create Spy Game Canvas");
        Undo.AddComponent<SpyGameUiRootMarker>(root);

        Canvas canvas = Undo.AddComponent<Canvas>(root);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = Undo.AddComponent<CanvasScaler>(root);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Undo.AddComponent<GraphicRaycaster>(root);
        Undo.AddComponent<HUDManager>(root);

        RectTransform rt = root.GetComponent<RectTransform>();
        StretchFullScreen(rt);

        return root;
    }

    private static GameObject CreateHudRoot(Transform canvas)
    {
        GameObject hud = new GameObject("HUD", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(hud, "Create HUD");
        Undo.SetTransformParent(hud.transform, canvas, "HUD Parent");
        StretchFullScreen(hud.GetComponent<RectTransform>());
        return hud;
    }

    private static Image CreateCrosshair(Transform hud)
    {
        GameObject go = new GameObject("Crosshair", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Create Crosshair");
        Undo.SetTransformParent(go.transform, hud, "Crosshair Parent");

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(6f, 6f);
        rt.anchoredPosition = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.sprite = CreateWhiteSprite();
        img.color = new Color(0.85f, 0.92f, 0.95f, 0.75f);
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI CreateObjectiveText(Transform hud)
    {
        GameObject go = new GameObject("Objective", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Create Objective");
        Undo.SetTransformParent(go.transform, hud, "Objective Parent");

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(900f, 48f);
        rt.anchoredPosition = new Vector2(0f, -20f);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        ApplyDefaultFontPublic(tmp);
        tmp.text = "// OBJECTIVE PENDING";
        tmp.fontSize = 22f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.75f, 0.88f, 0.92f, 0.95f);
        tmp.fontStyle = FontStyles.UpperCase;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    private static void CreateSuspicionMeter(Transform hud, out SuspicionBarUI bar, out SuspicionUIController controller, out TextMeshProUGUI header)
    {
        GameObject root = new GameObject("SuspicionMeter", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create Suspicion Meter");
        Undo.SetTransformParent(root.transform, hud, "Suspicion Parent");

        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0f, 1f);
        rootRt.anchorMax = new Vector2(0f, 1f);
        rootRt.pivot = new Vector2(0f, 1f);
        rootRt.sizeDelta = new Vector2(340f, 96f);
        rootRt.anchoredPosition = new Vector2(20f, -16f);

        GameObject back = new GameObject("Backplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(back, "Suspicion Backplate");
        Undo.SetTransformParent(back.transform, root.transform, "Suspicion Backplate Parent");
        StretchFullScreen(back.GetComponent<RectTransform>());
        Image suspicionBackImg = back.GetComponent<Image>();
        suspicionBackImg.color = new Color(0.05f, 0.07f, 0.1f, 0.55f);
        suspicionBackImg.raycastTarget = false;

        GameObject headerGo = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(headerGo, "Suspicion Header");
        Undo.SetTransformParent(headerGo.transform, root.transform, "Suspicion Header Parent");
        RectTransform headerRt = headerGo.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0f, 1f);
        headerRt.sizeDelta = new Vector2(-16f, 22f);
        headerRt.anchoredPosition = new Vector2(8f, -6f);
        header = headerGo.GetComponent<TextMeshProUGUI>();
        ApplyDefaultFontPublic(header);
        header.text = "TRUST  |  SUSPICION";
        header.fontSize = 14f;
        header.alignment = TextAlignmentOptions.Left;
        header.color = new Color(0.65f, 0.78f, 0.86f, 0.9f);
        header.fontStyle = FontStyles.Bold;

        GameObject trackGo = new GameObject("TrustSuspicionTrack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(trackGo, "Create Trust Suspicion Track");
        Undo.SetTransformParent(trackGo.transform, root.transform, "Trust Suspicion Track Parent");
        RectTransform trackRt = trackGo.GetComponent<RectTransform>();
        trackRt.anchorMin = new Vector2(0f, 0.5f);
        trackRt.anchorMax = new Vector2(1f, 0.5f);
        trackRt.pivot = new Vector2(0.5f, 0.5f);
        trackRt.offsetMin = new Vector2(14f, -8f);
        trackRt.offsetMax = new Vector2(-14f, 8f);
        Image trackImage = trackGo.GetComponent<Image>();
        trackImage.color = new Color(0.12f, 0.16f, 0.2f, 0.95f);
        trackImage.raycastTarget = false;

        GameObject centerGo = new GameObject("NeutralCenterMarker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(centerGo, "Create Neutral Center Marker");
        Undo.SetTransformParent(centerGo.transform, trackGo.transform, "Neutral Center Marker Parent");
        RectTransform centerRt = centerGo.GetComponent<RectTransform>();
        centerRt.anchorMin = new Vector2(0.5f, 0.5f);
        centerRt.anchorMax = new Vector2(0.5f, 0.5f);
        centerRt.pivot = new Vector2(0.5f, 0.5f);
        centerRt.sizeDelta = new Vector2(3f, 28f);
        centerRt.anchoredPosition = Vector2.zero;
        Image centerImage = centerGo.GetComponent<Image>();
        centerImage.color = new Color(0.75f, 0.82f, 0.85f, 1f);
        centerImage.raycastTarget = false;

        GameObject indicatorGo = new GameObject("TrustSuspicionIndicator", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(indicatorGo, "Create Trust Suspicion Indicator");
        Undo.SetTransformParent(indicatorGo.transform, trackGo.transform, "Trust Suspicion Indicator Parent");
        RectTransform indicatorRt = indicatorGo.GetComponent<RectTransform>();
        indicatorRt.anchorMin = new Vector2(0.5f, 0.5f);
        indicatorRt.anchorMax = new Vector2(0.5f, 0.5f);
        indicatorRt.pivot = new Vector2(0.5f, 0.5f);
        indicatorRt.sizeDelta = new Vector2(12f, 34f);
        indicatorRt.anchoredPosition = Vector2.zero;
        Image indicatorImage = indicatorGo.GetComponent<Image>();
        indicatorImage.color = new Color(0.75f, 0.82f, 0.85f, 1f);
        indicatorImage.raycastTarget = false;

        GameObject levelGo = new GameObject("LevelLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(levelGo, "Suspicion Level");
        Undo.SetTransformParent(levelGo.transform, root.transform, "Suspicion Level Parent");
        RectTransform levelRt = levelGo.GetComponent<RectTransform>();
        levelRt.anchorMin = new Vector2(1f, 1f);
        levelRt.anchorMax = new Vector2(1f, 1f);
        levelRt.pivot = new Vector2(1f, 1f);
        levelRt.sizeDelta = new Vector2(150f, 22f);
        levelRt.anchoredPosition = new Vector2(-8f, -6f);
        TextMeshProUGUI levelTmp = levelGo.GetComponent<TextMeshProUGUI>();
        ApplyDefaultFontPublic(levelTmp);
        levelTmp.text = "Neutral";
        levelTmp.fontSize = 13f;
        levelTmp.alignment = TextAlignmentOptions.Right;
        levelTmp.color = new Color(0.85f, 0.9f, 0.95f, 0.95f);

        GameObject valueGo = new GameObject("ValueLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(valueGo, "Suspicion Value");
        Undo.SetTransformParent(valueGo.transform, root.transform, "Suspicion Value Parent");
        RectTransform valueRt = valueGo.GetComponent<RectTransform>();
        valueRt.anchorMin = new Vector2(0f, 0f);
        valueRt.anchorMax = new Vector2(0f, 0f);
        valueRt.pivot = new Vector2(0f, 0f);
        valueRt.sizeDelta = new Vector2(80f, 22f);
        valueRt.anchoredPosition = new Vector2(12f, 8f);
        TextMeshProUGUI valueTmp = valueGo.GetComponent<TextMeshProUGUI>();
        ApplyDefaultFontPublic(valueTmp);
        valueTmp.text = "0";
        valueTmp.fontSize = 13f;
        valueTmp.alignment = TextAlignmentOptions.Left;
        valueTmp.color = new Color(0.85f, 0.9f, 0.95f, 0.95f);

        controller = Undo.AddComponent<SuspicionUIController>(root);
        bar = Undo.AddComponent<SuspicionBarUI>(root);

        BindSerializedPublic(
            bar,
            ("trackRect", trackRt),
            ("indicatorRect", indicatorRt),
            ("centerMarkerRect", centerRt),
            ("levelLabel", levelTmp),
            ("valueLabel", valueTmp),
            ("hideWhenClear", false));
    }

    private static void CreateInteractionPrompt(Transform hud, out InteractionPromptUI promptUi)
    {
        GameObject root = new GameObject("InteractionPrompt", typeof(RectTransform), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(root, "Create Interaction Prompt");
        Undo.SetTransformParent(root.transform, hud, "Interaction Prompt Parent");

        RectTransform rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(720f, 56f);
        rt.anchoredPosition = new Vector2(0f, 28f);

        CanvasGroup cg = root.GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        GameObject textGo = new GameObject("PromptText", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(textGo, "Interaction Prompt Text");
        Undo.SetTransformParent(textGo.transform, root.transform, "Interaction Prompt Text Parent");
        StretchFullScreen(textGo.GetComponent<RectTransform>());
        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        ApplyDefaultFontPublic(tmp);
        tmp.text = string.Empty;
        tmp.fontSize = 20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.9f, 0.93f, 0.96f, 0.95f);
        tmp.textWrappingMode = TextWrappingModes.Normal;

        promptUi = Undo.AddComponent<InteractionPromptUI>(root);
        BindSerializedPublic(
            promptUi,
            ("canvasGroup", cg),
            ("promptText", tmp),
            ("promptFormat", "Press E to {0}"));
    }

    private static void CreateEvidenceNotification(Transform hud, out EvidenceNotificationUI notification)
    {
        GameObject root = new GameObject("EvidenceNotification", typeof(RectTransform), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(root, "Evidence Notification");
        Undo.SetTransformParent(root.transform, hud, "Evidence Notification Parent");

        RectTransform rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(380f, 96f);
        rt.anchoredPosition = new Vector2(-20f, -120f);

        CanvasGroup cg = root.GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        GameObject back = new GameObject("Backplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(back, "Evidence Toast Backplate");
        Undo.SetTransformParent(back.transform, root.transform, "Evidence Toast Backplate Parent");
        StretchFullScreen(back.GetComponent<RectTransform>());
        Image toastBack = back.GetComponent<Image>();
        toastBack.color = new Color(0.08f, 0.1f, 0.14f, 0.85f);
        toastBack.raycastTarget = false;

        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(iconGo, "Evidence Icon");
        Undo.SetTransformParent(iconGo.transform, root.transform, "Evidence Icon Parent");
        RectTransform iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.sizeDelta = new Vector2(48f, 48f);
        iconRt.anchoredPosition = new Vector2(12f, 0f);
        Image icon = iconGo.GetComponent<Image>();
        icon.color = new Color(1f, 1f, 1f, 0.15f);
        icon.enabled = false;

        GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(titleGo, "Evidence Title");
        Undo.SetTransformParent(titleGo.transform, root.transform, "Evidence Title Parent");
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.5f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0f, 1f);
        titleRt.offsetMin = new Vector2(68f, 0f);
        titleRt.offsetMax = new Vector2(-8f, -8f);
        TextMeshProUGUI titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        ApplyDefaultFontPublic(titleTmp);
        titleTmp.text = "EVIDENCE ACQUIRED";
        titleTmp.fontSize = 16f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = new Color(0.92f, 0.95f, 0.98f, 1f);
        titleTmp.alignment = TextAlignmentOptions.Left;

        GameObject catGo = new GameObject("Category", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(catGo, "Evidence Category");
        Undo.SetTransformParent(catGo.transform, root.transform, "Evidence Category Parent");
        RectTransform catRt = catGo.GetComponent<RectTransform>();
        catRt.anchorMin = new Vector2(0f, 0f);
        catRt.anchorMax = new Vector2(1f, 0.5f);
        catRt.pivot = new Vector2(0f, 0f);
        catRt.offsetMin = new Vector2(68f, 8f);
        catRt.offsetMax = new Vector2(-8f, -4f);
        TextMeshProUGUI catTmp = catGo.GetComponent<TextMeshProUGUI>();
        ApplyDefaultFontPublic(catTmp);
        catTmp.text = string.Empty;
        catTmp.fontSize = 13f;
        catTmp.color = new Color(0.7f, 0.8f, 0.88f, 0.95f);
        catTmp.alignment = TextAlignmentOptions.Left;

        notification = Undo.AddComponent<EvidenceNotificationUI>(root);
        BindSerializedPublic(
            notification,
            ("panel", cg),
            ("iconImage", icon),
            ("titleText", titleTmp),
            ("categoryText", catTmp),
            ("visibleSeconds", 2.5f),
            ("fadeOutSeconds", 0.35f));
    }

    private static void CreateDialogueStack(
        Transform canvas,
        out DialogueUIView dialogueView,
        out DialogueUIController dialogueController,
        out Button evidenceJournalButton)
    {
        GameObject stack = new GameObject("Dialogue_UI", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(stack, "Create Dialogue UI");
        Undo.SetTransformParent(stack.transform, canvas, "Dialogue UI Parent");
        RectTransform stackRt = stack.GetComponent<RectTransform>();
        stackRt.anchorMin = new Vector2(0f, 0f);
        stackRt.anchorMax = new Vector2(1f, 0f);
        stackRt.pivot = new Vector2(0.5f, 0f);
        stackRt.sizeDelta = new Vector2(-48f, 280f);
        stackRt.anchoredPosition = new Vector2(0f, 12f);

        GameObject panel = new GameObject("Dialogue_Panel", typeof(RectTransform), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(panel, "Dialogue Panel");
        Undo.SetTransformParent(panel.transform, stack.transform, "Dialogue Panel Parent");
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = new Vector2(0f, 52f);
        panelRt.offsetMax = new Vector2(0f, 0f);

        CanvasGroup panelGroup = panel.GetComponent<CanvasGroup>();
        panelGroup.alpha = 0f;
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable = false;

        Image panelFrame = Undo.AddComponent<Image>(panel);
        panelFrame.color = new Color(0.04f, 0.06f, 0.09f, 0.92f);
        // Do not steal raycasts from Continue / evidence strip; children handle interaction when visible.
        panelFrame.raycastTarget = false;

        GameObject speakerGo = new GameObject("SpeakerName", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(speakerGo, "Speaker Name");
        Undo.SetTransformParent(speakerGo.transform, panel.transform, "Speaker Parent");
        RectTransform speakerRt = speakerGo.GetComponent<RectTransform>();
        speakerRt.anchorMin = new Vector2(0f, 1f);
        speakerRt.anchorMax = new Vector2(1f, 1f);
        speakerRt.pivot = new Vector2(0f, 1f);
        speakerRt.sizeDelta = new Vector2(-24f, 28f);
        speakerRt.anchoredPosition = new Vector2(12f, -10f);
        TextMeshProUGUI speakerTmp = speakerGo.GetComponent<TextMeshProUGUI>();
        ApplyDefaultFontPublic(speakerTmp);
        speakerTmp.text = "NPC NAME";
        speakerTmp.fontSize = 18f;
        speakerTmp.fontStyle = FontStyles.Bold;
        speakerTmp.alignment = TextAlignmentOptions.Left;
        speakerTmp.color = new Color(0.55f, 0.82f, 0.95f, 1f);

        GameObject evidenceStrip = new GameObject("EvidenceStrip", typeof(RectTransform), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(evidenceStrip, "Evidence Strip");
        Undo.SetTransformParent(evidenceStrip.transform, panel.transform, "Evidence Strip Parent");
        RectTransform stripRt = evidenceStrip.GetComponent<RectTransform>();
        stripRt.anchorMin = new Vector2(0f, 1f);
        stripRt.anchorMax = new Vector2(1f, 1f);
        stripRt.pivot = new Vector2(0.5f, 1f);
        stripRt.sizeDelta = new Vector2(-24f, 76f);
        stripRt.anchoredPosition = new Vector2(0f, -42f);
        CanvasGroup stripGroup = evidenceStrip.GetComponent<CanvasGroup>();
        stripGroup.alpha = 0f;

        GameObject evIcon = new GameObject("EvidenceIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(evIcon, "Dialogue Evidence Icon");
        Undo.SetTransformParent(evIcon.transform, evidenceStrip.transform, "Evidence Icon Parent");
        RectTransform evIconRt = evIcon.GetComponent<RectTransform>();
        evIconRt.anchorMin = evIconRt.anchorMax = new Vector2(0f, 0.5f);
        evIconRt.pivot = new Vector2(0f, 0.5f);
        evIconRt.sizeDelta = new Vector2(56f, 56f);
        evIconRt.anchoredPosition = new Vector2(8f, 0f);
        Image evIconImg = evIcon.GetComponent<Image>();
        evIconImg.enabled = false;

        GameObject evName = new GameObject("EvidenceName", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(evName, "Evidence Name");
        Undo.SetTransformParent(evName.transform, evidenceStrip.transform, "Evidence Name Parent");
        RectTransform evNameRt = evName.GetComponent<RectTransform>();
        evNameRt.anchorMin = new Vector2(0f, 0.5f);
        evNameRt.anchorMax = new Vector2(1f, 1f);
        evNameRt.pivot = new Vector2(0f, 1f);
        evNameRt.offsetMin = new Vector2(72f, 4f);
        evNameRt.offsetMax = new Vector2(-8f, -6f);
        TextMeshProUGUI evNameTmp = evName.GetComponent<TextMeshProUGUI>();
        ApplyDefaultFontPublic(evNameTmp);
        evNameTmp.fontSize = 15f;
        evNameTmp.fontStyle = FontStyles.Bold;
        evNameTmp.alignment = TextAlignmentOptions.Left;

        GameObject evCat = new GameObject("EvidenceCategory", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(evCat, "Evidence Category");
        Undo.SetTransformParent(evCat.transform, evidenceStrip.transform, "Evidence Category Parent");
        RectTransform evCatRt = evCat.GetComponent<RectTransform>();
        evCatRt.anchorMin = new Vector2(0f, 0f);
        evCatRt.anchorMax = new Vector2(0.45f, 0.5f);
        evCatRt.offsetMin = new Vector2(72f, 6f);
        evCatRt.offsetMax = new Vector2(-4f, -2f);
        TextMeshProUGUI evCatTmp = evCat.GetComponent<TextMeshProUGUI>();
        ApplyDefaultFontPublic(evCatTmp);
        evCatTmp.fontSize = 12f;
        evCatTmp.alignment = TextAlignmentOptions.Left;

        GameObject evDesc = new GameObject("EvidenceDescription", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(evDesc, "Evidence Description");
        Undo.SetTransformParent(evDesc.transform, evidenceStrip.transform, "Evidence Description Parent");
        RectTransform evDescRt = evDesc.GetComponent<RectTransform>();
        evDescRt.anchorMin = new Vector2(0.45f, 0f);
        evDescRt.anchorMax = new Vector2(1f, 1f);
        evDescRt.offsetMin = new Vector2(4f, 6f);
        evDescRt.offsetMax = new Vector2(-8f, -6f);
        TextMeshProUGUI evDescTmp = evDesc.GetComponent<TextMeshProUGUI>();
        ApplyDefaultFontPublic(evDescTmp);
        evDescTmp.fontSize = 12f;
        evDescTmp.textWrappingMode = TextWrappingModes.Normal;
        evDescTmp.alignment = TextAlignmentOptions.TopLeft;

        GameObject bodyGo = new GameObject("DialogueBody", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(bodyGo, "Dialogue Body");
        Undo.SetTransformParent(bodyGo.transform, panel.transform, "Dialogue Body Parent");
        RectTransform bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.offsetMin = new Vector2(12f, 52f);
        bodyRt.offsetMax = new Vector2(-12f, -124f);
        TextMeshProUGUI bodyTmp = bodyGo.GetComponent<TextMeshProUGUI>();
        ApplyDefaultFontPublic(bodyTmp);
        bodyTmp.fontSize = 20f;
        bodyTmp.textWrappingMode = TextWrappingModes.Normal;
        bodyTmp.alignment = TextAlignmentOptions.TopLeft;
        bodyTmp.color = new Color(0.92f, 0.94f, 0.96f, 1f);
        // Typewriter uses maxVisibleCharacters; Ellipsis overflow can fight partial reveals.
        bodyTmp.overflowMode = TextOverflowModes.Overflow;

        GameObject continueGo = DefaultControls.CreateButton(CreateUiResourcesPublic());
        continueGo.name = "ContinueButton";
        Undo.RegisterCreatedObjectUndo(continueGo, "Continue Button");
        Undo.SetTransformParent(continueGo.transform, panel.transform, "Continue Parent");
        RectTransform continueRt = continueGo.GetComponent<RectTransform>();
        continueRt.anchorMin = new Vector2(0f, 0f);
        continueRt.anchorMax = new Vector2(1f, 0f);
        continueRt.pivot = new Vector2(0.5f, 0f);
        continueRt.sizeDelta = new Vector2(-24f, 40f);
        continueRt.anchoredPosition = new Vector2(0f, 8f);
        TextMeshProUGUI continueLabel = continueGo.GetComponentInChildren<TextMeshProUGUI>();
        if (continueLabel != null)
        {
            ApplyDefaultFontPublic(continueLabel);
            continueLabel.text = "CONTINUE";
            continueLabel.fontSize = 16f;
            continueLabel.fontStyle = FontStyles.Bold;
        }

        Button continueButton = continueGo.GetComponent<Button>();
        ColorBlock colors = continueButton.colors;
        colors.highlightedColor = new Color(0.35f, 0.55f, 0.7f, 1f);
        colors.normalColor = new Color(0.22f, 0.32f, 0.42f, 1f);
        continueButton.colors = colors;

        GameObject stripButtons = new GameObject("BottomStrip", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(stripButtons, "Dialogue Bottom Strip");
        Undo.SetTransformParent(stripButtons.transform, stack.transform, "Bottom Strip Parent");
        RectTransform stripRt2 = stripButtons.GetComponent<RectTransform>();
        stripRt2.anchorMin = Vector2.zero;
        stripRt2.anchorMax = new Vector2(1f, 0f);
        stripRt2.pivot = new Vector2(0.5f, 0f);
        stripRt2.sizeDelta = new Vector2(0f, 44f);
        stripRt2.anchoredPosition = Vector2.zero;

        GameObject evidenceBtnGo = DefaultControls.CreateButton(CreateUiResourcesPublic());
        evidenceBtnGo.name = "EvidenceJournalButton";
        Undo.RegisterCreatedObjectUndo(evidenceBtnGo, "Evidence Journal Button");
        Undo.SetTransformParent(evidenceBtnGo.transform, stripButtons.transform, "Evidence Journal Parent");
        RectTransform ebr = evidenceBtnGo.GetComponent<RectTransform>();
        ebr.anchorMin = new Vector2(1f, 0f);
        ebr.anchorMax = new Vector2(1f, 1f);
        ebr.pivot = new Vector2(1f, 0.5f);
        ebr.sizeDelta = new Vector2(200f, 0f);
        ebr.offsetMin = new Vector2(-220f, 4f);
        ebr.offsetMax = new Vector2(-12f, -4f);
        TextMeshProUGUI ejLabel = evidenceBtnGo.GetComponentInChildren<TextMeshProUGUI>();
        if (ejLabel != null)
        {
            ApplyDefaultFontPublic(ejLabel);
            ejLabel.text = "EVIDENCE LOG";
            ejLabel.fontSize = 14f;
            ejLabel.fontStyle = FontStyles.Bold;
        }

        evidenceJournalButton = evidenceBtnGo.GetComponent<Button>();

        dialogueView = Undo.AddComponent<DialogueUIView>(panel);
        BindSerializedPublic(
            dialogueView,
            ("panelCanvasGroup", panelGroup),
            ("speakerNameText", speakerTmp),
            ("dialogueBodyText", bodyTmp),
            ("continueButton", continueButton),
            ("charactersPerSecond", 42f),
            ("evidenceCanvasGroup", stripGroup),
            ("evidenceIcon", evIconImg),
            ("evidenceNameText", evNameTmp),
            ("evidenceCategoryText", evCatTmp),
            ("evidenceDescriptionText", evDescTmp));

        dialogueController = Undo.AddComponent<DialogueUIController>(stack);
    }

    private static void WireManagersInScene(DialogueUIView dialogueView)
    {
        DialogueManager dialogueManager = UnityEngine.Object.FindAnyObjectByType<DialogueManager>();
        if (dialogueManager == null)
        {
            Debug.LogWarning("[SpyGameUiBuilder] No DialogueManager in scene. Add one and assign Dialogue View manually.");
            return;
        }

        SerializedObject so = new SerializedObject(dialogueManager);
        so.FindProperty("dialogueView").objectReferenceValue = dialogueView;
        DialoguePlayerLock playerLock = UnityEngine.Object.FindAnyObjectByType<DialoguePlayerLock>();
        if (playerLock != null)
        {
            so.FindProperty("playerLock").objectReferenceValue = playerLock;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(dialogueManager);
    }

    private static void TryWirePlayerInteractor(InteractionPromptUI promptUi)
    {
        PlayerInteractor[] interactors = UnityEngine.Object.FindObjectsByType<PlayerInteractor>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (interactors.Length == 0)
        {
            Debug.LogWarning("[SpyGameUiBuilder] No PlayerInteractor found. Assign Interaction Prompt UI on the player.");
            return;
        }

        if (interactors.Length > 1)
        {
            Debug.LogWarning("[SpyGameUiBuilder] Multiple PlayerInteractor components found. Assign prompt UI manually.");
            return;
        }

        SerializedObject so = new SerializedObject(interactors[0]);
        so.FindProperty("promptUI").objectReferenceValue = promptUi;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(interactors[0]);
    }

    private static void SavePrefab(GameObject canvasRoot)
    {
        string directory = System.IO.Path.GetDirectoryName(PrefabPath);
        if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory.Replace("\\", "/")))
        {
            string parent = "Assets/Prefabs";
            if (!AssetDatabase.IsValidFolder(parent))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            AssetDatabase.CreateFolder(parent, "UI");
        }

        PrefabUtility.SaveAsPrefabAsset(canvasRoot, PrefabPath);
        AssetDatabase.Refresh();
        Debug.Log("[SpyGameUiBuilder] Prefab saved to " + PrefabPath);
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

    public static void ApplyDefaultFontPublic(TextMeshProUGUI tmp)
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font != null)
        {
            tmp.font = font;
        }
    }

    private static Sprite CreateWhiteSprite()
    {
        Texture2D tex = Texture2D.whiteTexture;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    public static DefaultControls.Resources CreateUiResourcesPublic()
    {
        Sprite fallback = CreateWhiteSprite();
        return new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd") ?? fallback,
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd") ?? fallback,
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd") ?? fallback,
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd") ?? fallback,
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd") ?? fallback,
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd") ?? fallback,
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd") ?? fallback
        };
    }

    public static void BindSerializedPublic(UnityEngine.Object target, params (string name, object value)[] pairs)
    {
        if (target == null)
        {
            return;
        }

        SerializedObject so = new SerializedObject(target);
        foreach ((string name, object value) in pairs)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop == null)
            {
                Debug.LogWarning($"[SpyGameUiBuilder] Missing serialized field '{name}' on {target.GetType().Name}.");
                continue;
            }

            switch (prop.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = value as UnityEngine.Object;
                    break;
                case SerializedPropertyType.Boolean:
                    if (value is bool booleanValue)
                    {
                        prop.boolValue = booleanValue;
                    }

                    break;
                case SerializedPropertyType.Float:
                    if (value is float floatValue)
                    {
                        prop.floatValue = floatValue;
                    }

                    break;
                case SerializedPropertyType.Integer:
                    if (value is int intValue)
                    {
                        prop.intValue = intValue;
                    }

                    break;
                case SerializedPropertyType.String:
                    if (value is string stringValue)
                    {
                        prop.stringValue = stringValue;
                    }

                    break;
                default:
                    Debug.LogWarning($"[SpyGameUiBuilder] Unsupported property type {prop.propertyType} for '{name}'.");
                    break;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
