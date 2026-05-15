using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Validates and optionally repairs spy-game scene wiring (managers, UI, NPCs, interaction).
/// </summary>
public static class SpyGameSceneArchitectureValidator
{
    private const string QuestionPanelName = "DialogueQuestionInputView";

    [MenuItem("Tools/Spy Game/Validate Scene Architecture", priority = 200)]
    public static void ValidateSceneMenu()
    {
        ValidationReport report = ValidateActiveScene();
        ShowReport(report, "Validation complete.");
    }

    [MenuItem("Tools/Spy Game/Validate And Fix Safe Issues", priority = 201)]
    public static void ValidateAndFixMenu()
    {
        ValidationReport report = ValidateActiveScene();
        int fixedCount = ApplySafeFixes(report);
        report = ValidateActiveScene();
        ShowReport(report, $"Applied {fixedCount} safe fix(es). Re-validated.");
    }

    public static ValidationReport ValidateActiveScene()
    {
        ValidationReport report = new ValidationReport();

        DialogueManager[] dialogueManagers = Object.FindObjectsByType<DialogueManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (dialogueManagers.Length == 0)
        {
            report.Errors.Add("No DialogueManager in scene.");
        }
        else if (dialogueManagers.Length > 1)
        {
            report.Errors.Add($"Found {dialogueManagers.Length} DialogueManager instances (expect 1).");
        }
        else
        {
            ValidateDialogueManager(dialogueManagers[0], report);
        }

        SuspicionManager[] suspicionManagers = Object.FindObjectsByType<SuspicionManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (suspicionManagers.Length == 0)
        {
            report.Warnings.Add("No SuspicionManager in scene (NPC prompts still work; suspicion UI may not).");
        }
        else if (suspicionManagers.Length > 1)
        {
            report.Errors.Add($"Found {suspicionManagers.Length} SuspicionManager instances (expect 1).");
        }

        EvidenceInventory[] inventories = Object.FindObjectsByType<EvidenceInventory>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (inventories.Length == 0)
        {
            report.Warnings.Add("No EvidenceInventory in scene.");
        }
        else if (inventories.Length > 1)
        {
            report.Warnings.Add($"Found {inventories.Length} EvidenceInventory instances.");
        }
        else
        {
            ValidateEvidenceSuspicionIntegration(inventories[0], report);
        }

        EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (eventSystems.Length == 0)
        {
            report.Errors.Add("No EventSystem — UI buttons and TMP input will not work.");
        }
        else if (eventSystems.Length > 1)
        {
            report.Errors.Add($"Found {eventSystems.Length} EventSystems (keep exactly one).");
        }

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (canvases.Length == 0)
        {
            report.Errors.Add("No Canvas in scene.");
        }

        DialogueQuestionInputView questionView = Object.FindFirstObjectByType<DialogueQuestionInputView>(
            FindObjectsInactive.Include);
        if (questionView == null)
        {
            report.Errors.Add(
                $"Missing '{QuestionPanelName}' under your HUD canvas. Run Tools → Add Dialogue Question Input To Canvas.");
        }
        else
        {
            ValidateQuestionInputView(questionView, report);
        }

        PlayerInteractor interactor = Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        if (interactor == null)
        {
            report.Errors.Add("No PlayerInteractor on Player.");
        }
        else
        {
            ValidatePlayerInteractor(interactor, report);
        }

        DialoguePlayerLock playerLock = Object.FindFirstObjectByType<DialoguePlayerLock>(FindObjectsInactive.Include);
        if (playerLock == null)
        {
            report.Errors.Add("No DialoguePlayerLock on Player.");
        }
        else
        {
            ValidatePlayerLock(playerLock, report);
        }

        NPCBrain[] brains = Object.FindObjectsByType<NPCBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (brains.Length == 0)
        {
            report.Warnings.Add("No NPCBrain components in scene.");
        }

        foreach (NPCBrain brain in brains)
        {
            ValidateNpcBrain(brain, questionView, playerLock, report);
        }

        DialogueUIView dialogueView = Object.FindFirstObjectByType<DialogueUIView>(FindObjectsInactive.Include);
        if (dialogueView == null)
        {
            report.Errors.Add("No DialogueUIView in scene.");
        }
        else
        {
            ValidateDialogueView(dialogueView, report);
        }

        HUDManager hud = Object.FindFirstObjectByType<HUDManager>(FindObjectsInactive.Include);
        if (hud != null)
        {
            ValidateHudManager(hud, report);
        }

        return report;
    }

    public static int ApplySafeFixes(ValidationReport report)
    {
        int count = 0;

        DialoguePlayerLock playerLock = Object.FindFirstObjectByType<DialoguePlayerLock>(FindObjectsInactive.Include);
        DialogueQuestionInputView questionView = Object.FindFirstObjectByType<DialogueQuestionInputView>(
            FindObjectsInactive.Include);

        if (questionView == null)
        {
            Canvas canvas = Object.FindFirstObjectByType<SpyGameUiRootMarker>()?.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = Object.FindFirstObjectByType<Canvas>();
            }

            if (canvas != null)
            {
                questionView = DialogueQuestionInputViewBuilder.BuildPanel(canvas.transform);
                DialogueQuestionInputViewEditor.AutoWire(questionView);
                count++;
                Debug.Log("[SpyGameValidator] Created DialogueQuestionInputView under existing canvas.");
            }
        }

        DialogueManager dialogueManager = Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
        if (dialogueManager != null)
        {
            SerializedObject dmSo = new SerializedObject(dialogueManager);
            if (dmSo.FindProperty("dialogueView").objectReferenceValue == null)
            {
                DialogueUIView view = Object.FindFirstObjectByType<DialogueUIView>(FindObjectsInactive.Include);
                if (view != null)
                {
                    dmSo.FindProperty("dialogueView").objectReferenceValue = view;
                    count++;
                }
            }

            if (playerLock != null && dmSo.FindProperty("playerLock").objectReferenceValue == null)
            {
                dmSo.FindProperty("playerLock").objectReferenceValue = playerLock;
                count++;
            }

            dmSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(dialogueManager);
        }

        if (playerLock != null)
        {
            SerializedObject lockSo = new SerializedObject(playerLock);
            if (lockSo.FindProperty("firstPersonController").objectReferenceValue == null)
            {
                FirstPersonController fpc = playerLock.GetComponent<FirstPersonController>();
                if (fpc != null)
                {
                    lockSo.FindProperty("firstPersonController").objectReferenceValue = fpc;
                    count++;
                }
            }

            if (lockSo.FindProperty("playerInteractor").objectReferenceValue == null)
            {
                PlayerInteractor pi = playerLock.GetComponent<PlayerInteractor>();
                if (pi != null)
                {
                    lockSo.FindProperty("playerInteractor").objectReferenceValue = pi;
                    count++;
                }
            }

            if (lockSo.FindProperty("playerRigidbody").objectReferenceValue == null)
            {
                Rigidbody rb = playerLock.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    lockSo.FindProperty("playerRigidbody").objectReferenceValue = rb;
                    count++;
                }
            }

            lockSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(playerLock);
        }

        PlayerInteractor interactor = Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        if (interactor != null)
        {
            SerializedObject piSo = new SerializedObject(interactor);
            if (piSo.FindProperty("promptUI").objectReferenceValue == null)
            {
                InteractionPromptUI prompt = Object.FindFirstObjectByType<InteractionPromptUI>(FindObjectsInactive.Include);
                if (prompt != null)
                {
                    piSo.FindProperty("promptUI").objectReferenceValue = prompt;
                    count++;
                }
            }

            piSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(interactor);
        }

        if (questionView != null)
        {
            DialogueQuestionInputViewEditor.AutoWire(questionView);
            DialogueQuestionInputViewBuilder.TryWireNpcBrainsPublic(questionView);
            count++;
        }

        NPCBrain[] brains = Object.FindObjectsByType<NPCBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (NPCBrain brain in brains)
        {
            SerializedObject so = new SerializedObject(brain);
            bool dirty = false;

            if (playerLock != null && so.FindProperty("dialoguePlayerLock").objectReferenceValue == null)
            {
                so.FindProperty("dialoguePlayerLock").objectReferenceValue = playerLock;
                dirty = true;
                count++;
            }

            if (questionView != null && so.FindProperty("questionInputView").objectReferenceValue == null)
            {
                so.FindProperty("questionInputView").objectReferenceValue = questionView;
                dirty = true;
                count++;
            }

            if (dirty)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(brain);
            }
        }

        EvidenceInventory inventory = Object.FindFirstObjectByType<EvidenceInventory>(FindObjectsInactive.Include);
        if (inventory != null && inventory.GetComponent<EvidenceSuspicionBridge>() == null)
        {
            Undo.AddComponent<EvidenceSuspicionBridge>(inventory.gameObject);
            EditorUtility.SetDirty(inventory.gameObject);
            count++;
            Debug.Log("[SpyGameValidator] Added EvidenceSuspicionBridge to EvidenceInventory.");
        }

        if (count > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        return count;
    }

    private static void ValidateEvidenceSuspicionIntegration(EvidenceInventory inventory, ValidationReport report)
    {
        if (inventory.GetComponent<EvidenceSuspicionBridge>() == null)
        {
            report.Warnings.Add(
                "EvidenceInventory has no EvidenceSuspicionBridge — collecting evidence will not change suspicion.");
        }
        else
        {
            report.Ok.Add("EvidenceSuspicionBridge present on EvidenceInventory.");
        }

        if (SuspicionManager.Instance == null && Object.FindAnyObjectByType<SuspicionManager>() == null)
        {
            report.Warnings.Add("SuspicionManager missing — evidence modifiers will not apply.");
        }
    }

    private static void ValidateDialogueManager(DialogueManager manager, ValidationReport report)
    {
        SerializedObject so = new SerializedObject(manager);
        if (so.FindProperty("dialogueView").objectReferenceValue == null)
        {
            report.Errors.Add("DialogueManager.dialogueView is not assigned (runtime may FindObject).");
        }

        if (so.FindProperty("playerLock").objectReferenceValue == null)
        {
            report.Warnings.Add("DialogueManager.playerLock is not assigned (runtime may FindObject).");
        }
    }

    private static void ValidatePlayerLock(DialoguePlayerLock playerLock, ValidationReport report)
    {
        SerializedObject so = new SerializedObject(playerLock);
        if (so.FindProperty("firstPersonController").objectReferenceValue == null)
        {
            report.Warnings.Add("DialoguePlayerLock.firstPersonController missing.");
        }

        if (so.FindProperty("playerInteractor").objectReferenceValue == null)
        {
            report.Warnings.Add("DialoguePlayerLock.playerInteractor missing.");
        }

        if (so.FindProperty("playerRigidbody").objectReferenceValue == null)
        {
            report.Warnings.Add("DialoguePlayerLock.playerRigidbody missing.");
        }
        else
        {
            report.Ok.Add("DialoguePlayerLock references look assigned.");
        }
    }

    private static void ValidatePlayerInteractor(PlayerInteractor interactor, ValidationReport report)
    {
        SerializedObject so = new SerializedObject(interactor);
        if (so.FindProperty("inputActions").objectReferenceValue == null)
        {
            report.Errors.Add("PlayerInteractor.inputActions is not assigned.");
        }
        else
        {
            report.Ok.Add("PlayerInteractor InputActionAsset assigned.");
        }

        if (so.FindProperty("cameraTransform").objectReferenceValue == null)
        {
            report.Warnings.Add("PlayerInteractor.cameraTransform missing (may resolve at runtime).");
        }

        if (so.FindProperty("promptUI").objectReferenceValue == null)
        {
            report.Warnings.Add("PlayerInteractor.promptUI missing.");
        }
    }

    private static void ValidateNpcBrain(
        NPCBrain brain,
        DialogueQuestionInputView questionView,
        DialoguePlayerLock playerLock,
        ValidationReport report)
    {
        string label = brain.gameObject.name;
        SerializedObject so = new SerializedObject(brain);

        if (so.FindProperty("profile").objectReferenceValue == null)
        {
            report.Errors.Add($"[{label}] NPCBrain.profile is not assigned.");
        }

        if (!brain.GetComponent<Collider>() && !brain.GetComponentInChildren<Collider>())
        {
            report.Errors.Add($"[{label}] No Collider — PlayerInteractor raycast cannot hit this NPC.");
        }

        bool requireTyped = so.FindProperty("requireTypedQuestionBeforeAi").boolValue;
        Object qView = so.FindProperty("questionInputView").objectReferenceValue;
        if (requireTyped && qView == null && questionView == null)
        {
            report.Errors.Add(
                $"[{label}] requireTypedQuestionBeforeAi is ON but Question Input View is missing — typed questions will not open.");
        }
        else if (requireTyped && qView == null)
        {
            report.Warnings.Add($"[{label}] questionInputView not assigned (panel exists in scene; run Fix).");
        }

        if (so.FindProperty("dialoguePlayerLock").objectReferenceValue == null && playerLock != null)
        {
            report.Warnings.Add($"[{label}] dialoguePlayerLock not assigned (runtime FindObject fallback).");
        }

        report.Ok.Add($"[{label}] Ollama configured: {so.FindProperty("ollamaBaseUrl").stringValue} / {so.FindProperty("ollamaModel").stringValue}");
    }

    private static void ValidateQuestionInputView(DialogueQuestionInputView view, ValidationReport report)
    {
        SerializedObject so = new SerializedObject(view);
        if (so.FindProperty("rootGroup").objectReferenceValue == null)
        {
            report.Errors.Add("DialogueQuestionInputView.rootGroup missing.");
        }

        if (so.FindProperty("questionInputField").objectReferenceValue == null)
        {
            report.Errors.Add("DialogueQuestionInputView.questionInputField missing.");
        }

        if (so.FindProperty("submitButton").objectReferenceValue == null)
        {
            report.Errors.Add("DialogueQuestionInputView.submitButton missing.");
        }

        if (view.GetComponentInParent<Canvas>() == null)
        {
            report.Errors.Add("DialogueQuestionInputView is not under a Canvas.");
        }
        else
        {
            report.Ok.Add("DialogueQuestionInputView is under a Canvas.");
        }
    }

    private static void ValidateDialogueView(DialogueUIView view, ValidationReport report)
    {
        SerializedObject so = new SerializedObject(view);
        if (so.FindProperty("panelCanvasGroup").objectReferenceValue == null)
        {
            report.Errors.Add("DialogueUIView.panelCanvasGroup missing.");
        }

        if (so.FindProperty("continueButton").objectReferenceValue == null)
        {
            report.Errors.Add("DialogueUIView.continueButton missing.");
        }
        else
        {
            report.Ok.Add("DialogueUIView continue button wired.");
        }
    }

    private static void ValidateHudManager(HUDManager hud, ValidationReport report)
    {
        SerializedObject so = new SerializedObject(hud);
        if (so.FindProperty("interactionPrompt").objectReferenceValue == null)
        {
            report.Warnings.Add("HUDManager.interactionPrompt missing.");
        }

        if (so.FindProperty("suspicionUi").objectReferenceValue == null)
        {
            report.Warnings.Add("HUDManager.suspicionUi missing.");
        }
    }

    private static void ShowReport(ValidationReport report, string title)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(title);
        sb.AppendLine();

        AppendSection(sb, "ERRORS", report.Errors);
        AppendSection(sb, "WARNINGS", report.Warnings);
        AppendSection(sb, "OK", report.Ok);

        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog(
            "Spy Game Scene Architecture",
            sb.ToString(),
            "OK");
    }

    private static void AppendSection(StringBuilder sb, string header, List<string> lines)
    {
        sb.AppendLine($"=== {header} ({lines.Count}) ===");
        if (lines.Count == 0)
        {
            sb.AppendLine("  (none)");
        }
        else
        {
            foreach (string line in lines)
            {
                sb.AppendLine("  • " + line);
            }
        }

        sb.AppendLine();
    }

    public sealed class ValidationReport
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Ok { get; } = new List<string>();

        public bool IsClean => Errors.Count == 0;
    }
}
