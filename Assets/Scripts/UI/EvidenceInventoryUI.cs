using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Evidence journal UI. Press I to toggle, then select an entry to view full details.
/// </summary>
public class EvidenceInventoryUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private CanvasGroup rootGroup;

    [Header("List")]
    [SerializeField] private EvidenceSlotUI slotPrefab;
    [SerializeField] private Transform slotContainer;
    [SerializeField] private bool clearContainerOnRefresh = true;
    [SerializeField] private GameObject emptyStateObject;
    [SerializeField] private TextMeshProUGUI emptyStateText;

    [Header("Details")]
    [SerializeField] private TextMeshProUGUI detailTitleText;
    [SerializeField] private TextMeshProUGUI detailCategoryText;
    [SerializeField] private TextMeshProUGUI detailCollectedAtText;
    [SerializeField] private TextMeshProUGUI detailDescriptionText;
    [SerializeField] private Button closeButton;

    [Header("Player Lock")]
    [SerializeField] private FirstPersonController firstPersonController;
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private bool lockCursorWhenClosed = true;

    private readonly List<EvidenceSlotUI> spawnedSlots = new List<EvidenceSlotUI>();
    private bool isOpen;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (rootGroup == null)
        {
            rootGroup = GetComponent<CanvasGroup>();
        }

        if (rootGroup == null)
        {
            rootGroup = gameObject.AddComponent<CanvasGroup>();
        }

        ResolvePlayerReferences();
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseJournal);
        }

        SetVisible(false);
        ShowEmptyDetails();
    }

    private void OnEnable()
    {
        if (EvidenceInventory.Instance != null)
        {
            EvidenceInventory.Instance.InventoryChanged += HandleInventoryChanged;
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (EvidenceInventory.Instance != null)
        {
            EvidenceInventory.Instance.InventoryChanged -= HandleInventoryChanged;
        }
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseJournal);
        }
    }

    private void Update()
    {
        if (ShouldIgnoreJournalToggleInput())
        {
            return;
        }

        if (!WasJournalTogglePressed())
        {
            return;
        }

        ToggleJournal();
    }

    private void HandleInventoryChanged()
    {
        Refresh();
    }

    public void ToggleJournal()
    {
        if (isOpen)
        {
            CloseJournal();
        }
        else
        {
            OpenJournal();
        }
    }

    public void OpenJournal()
    {
        OpenJournal(allowDuringConversation: false);
    }

    public void OpenJournalFromConversation()
    {
        OpenJournal(allowDuringConversation: true);
    }

    private void OpenJournal(bool allowDuringConversation)
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.IsTerminalState)
        {
            return;
        }

        if (ShouldBlockJournalOpen() && !allowDuringConversation)
        {
            return;
        }

        if (isOpen)
        {
            return;
        }

        isOpen = true;
        SetVisible(true);
        SetPlayerControlEnabled(false);
        UnlockCursor();
        Refresh();
    }

    public void CloseJournal()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        SetVisible(false);

        if (!IsPauseMenuOpen() && !IsConversationActive())
        {
            SetPlayerControlEnabled(true);
        }

        if (lockCursorWhenClosed && !IsPauseMenuOpen() && !IsConversationActive())
        {
            LockCursor();
        }
    }

    public void Refresh()
    {
        if (slotContainer == null)
        {
            return;
        }

        if (EvidenceInventory.Instance == null)
        {
            SetEmptyState(true);
            ShowEmptyDetails();
            return;
        }

        if (clearContainerOnRefresh)
        {
            ClearSpawnedSlots();
        }

        IReadOnlyList<EvidenceCollectionRecord> records = EvidenceInventory.Instance.CollectedRecords;
        bool hasEvidence = records.Count > 0;
        SetEmptyState(!hasEvidence);

        for (int i = 0; i < records.Count; i++)
        {
            if (records[i]?.Evidence == null)
            {
                continue;
            }

            EvidenceSlotUI slot = CreateSlot();
            if (slot != null)
            {
                slot.Bind(records[i], SelectEvidence);
            }
        }

        if (!hasEvidence)
        {
            ShowEmptyDetails();
        }
        else if (detailTitleText != null && string.IsNullOrWhiteSpace(detailTitleText.text))
        {
            SelectEvidence(records[0], false);
        }
    }

    private EvidenceSlotUI CreateSlot()
    {
        if (slotPrefab == null)
        {
            return null;
        }

        EvidenceSlotUI slot = Instantiate(slotPrefab, slotContainer);
        spawnedSlots.Add(slot);
        return slot;
    }

    private void SelectEvidence(EvidenceCollectionRecord record)
    {
        SelectEvidence(record, true);
    }

    private void SelectEvidence(EvidenceCollectionRecord record, bool allowConversationInquiry)
    {
        EvidenceData evidence = record?.Evidence;
        if (evidence == null)
        {
            ShowEmptyDetails();
            return;
        }

        if (detailTitleText != null)
        {
            detailTitleText.text = evidence.DisplayName;
        }

        if (detailCategoryText != null)
        {
            detailCategoryText.text = FormatCategory(evidence.Category);
        }

        if (detailCollectedAtText != null)
        {
            detailCollectedAtText.text = record.CollectedAtDisplay;
        }

        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = evidence.Description;
        }

        NPCBrain inquiryTarget = NPCBrain.CurrentEvidenceInquiryTarget;
        if (allowConversationInquiry &&
            inquiryTarget != null &&
            IsConversationActive())
        {
            CloseJournal();
            inquiryTarget.AskAboutEvidence(evidence);
        }
    }

    private void ShowEmptyDetails()
    {
        if (detailTitleText != null)
        {
            detailTitleText.text = "No evidence selected";
        }

        if (detailCategoryText != null)
        {
            detailCategoryText.text = string.Empty;
        }

        if (detailCollectedAtText != null)
        {
            detailCollectedAtText.text = string.Empty;
        }

        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = "Collect evidence from documents, terminals, audio logs, and clues to review it here.";
        }
    }

    private void SetEmptyState(bool empty)
    {
        if (emptyStateObject != null)
        {
            emptyStateObject.SetActive(empty);
        }

        if (emptyStateText != null)
        {
            emptyStateText.text = "No evidence collected yet.";
        }
    }

    private void ClearSpawnedSlots()
    {
        for (int i = spawnedSlots.Count - 1; i >= 0; i--)
        {
            if (spawnedSlots[i] != null)
            {
                Destroy(spawnedSlots[i].gameObject);
            }
        }

        spawnedSlots.Clear();
    }

    private void SetVisible(bool visible)
    {
        if (rootGroup != null)
        {
            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.blocksRaycasts = visible;
            rootGroup.interactable = visible;
            return;
        }

        gameObject.SetActive(visible);
    }

    private void ResolvePlayerReferences()
    {
        if (firstPersonController == null)
        {
            firstPersonController = FindFirstObjectByType<FirstPersonController>();
        }

        if (playerInteractor == null)
        {
            playerInteractor = FindFirstObjectByType<PlayerInteractor>();
        }
    }

    private void SetPlayerControlEnabled(bool enabled)
    {
        if (firstPersonController != null)
        {
            firstPersonController.enabled = enabled;
        }

        if (playerInteractor != null)
        {
            playerInteractor.enabled = enabled;
        }
    }

    private static bool IsPauseMenuOpen()
    {
        return GameplayPauseController.Instance != null && GameplayPauseController.Instance.IsPaused;
    }

    private static bool IsDialogueOpen()
    {
        return DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive;
    }

    private static bool IsConversationActive()
    {
        if (IsDialogueOpen())
        {
            return true;
        }

        DialogueQuestionInputView questionInput = FindFirstObjectByType<DialogueQuestionInputView>(
            FindObjectsInactive.Include);
        return questionInput != null && questionInput.IsSessionActive;
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static string FormatCategory(EvidenceCategory category)
    {
        return Regex.Replace(category.ToString(), "([a-z])([A-Z])", "$1 $2");
    }

    public static bool ShouldIgnoreJournalToggleInput()
    {
        return ShouldBlockJournalOpen() || IsTextInputFocused();
    }

    public static bool ShouldBlockJournalOpen()
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.IsTerminalState)
        {
            return true;
        }

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            return true;
        }

        DialogueQuestionInputView questionInput = FindFirstObjectByType<DialogueQuestionInputView>(
            FindObjectsInactive.Include);
        return questionInput != null && questionInput.IsSessionActive;
    }

    public static bool IsTextInputFocused()
    {
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
        {
            return false;
        }

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        TMP_InputField tmpInput = selected.GetComponent<TMP_InputField>() ??
                                  selected.GetComponentInParent<TMP_InputField>();
        if (tmpInput != null && tmpInput.isFocused)
        {
            return true;
        }

        InputField legacyInput = selected.GetComponent<InputField>() ??
                                 selected.GetComponentInParent<InputField>();
        return legacyInput != null && legacyInput.isFocused;
    }

    public static bool WasJournalTogglePressed()
    {
        if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
        {
            return true;
        }

        try
        {
            return Input.GetKeyDown(KeyCode.I);
        }
        catch (InvalidOperationException)
        {
            // Legacy Input Manager can be disabled when the project uses the new Input System only.
            return false;
        }
    }

    public void RuntimeSetReferences(
        CanvasGroup group,
        EvidenceSlotUI prefab,
        Transform container,
        GameObject emptyObject,
        TextMeshProUGUI emptyText,
        TextMeshProUGUI titleText,
        TextMeshProUGUI categoryText,
        TextMeshProUGUI collectedAtText,
        TextMeshProUGUI descriptionText,
        Button close)
    {
        rootGroup = group;
        slotPrefab = prefab;
        slotContainer = container;
        emptyStateObject = emptyObject;
        emptyStateText = emptyText;
        detailTitleText = titleText;
        detailCategoryText = categoryText;
        detailCollectedAtText = collectedAtText;
        detailDescriptionText = descriptionText;
        closeButton = close;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseJournal);
            closeButton.onClick.AddListener(CloseJournal);
        }
    }

#if UNITY_EDITOR
    public void EditorSetReferences(
        CanvasGroup group,
        EvidenceSlotUI prefab,
        Transform container,
        GameObject emptyObject,
        TextMeshProUGUI emptyText,
        TextMeshProUGUI titleText,
        TextMeshProUGUI categoryText,
        TextMeshProUGUI collectedAtText,
        TextMeshProUGUI descriptionText,
        Button close)
    {
        RuntimeSetReferences(
            group,
            prefab,
            container,
            emptyObject,
            emptyText,
            titleText,
            categoryText,
            collectedAtText,
            descriptionText,
            close);
    }
#endif
}
