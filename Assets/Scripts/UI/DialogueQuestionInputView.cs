using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Typed-question overlay for AI NPC conversations.
/// </summary>
/// <remarks>
/// <b>Where to attach:</b> Add this component to the root GameObject named <c>DialogueQuestionInputView</c>
/// (under your main HUD Canvas, sibling to <c>Dialogue_UI</c>).
/// <para>
/// <b>Setup:</b> Run <i>Tools → Create Dialogue Question Input UI</i> to build hierarchy and wire references,
/// or use the Inspector <i>Auto Wire Children</i> button on this component.
/// </para>
/// <para>
/// <b>Runtime:</b> <see cref="NPCBrain"/> calls <see cref="BeginSession"/> when typed questions are enabled.
/// Use <see cref="Show"/> / <see cref="Hide"/> directly for custom flows.
/// </para>
/// </remarks>
[DisallowMultipleComponent]
public class DialogueQuestionInputView : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("CanvasGroup on RootGroup — controls visibility and raycasts without disabling the whole canvas.")]
    [SerializeField] private CanvasGroup rootGroup;

    [Tooltip("Displays the NPC / target name while the player types.")]
    [SerializeField] private TextMeshProUGUI npcLabel;

    [Tooltip("TMP field for the player's custom question.")]
    [SerializeField] private TMP_InputField questionInputField;

    [SerializeField] private Button submitButton;
    [SerializeField] private Button cancelButton;

    private Action<string> pendingSubmit;
    private Action pendingCancel;

    private void Awake()
    {
        WireButtonListeners();
        ConfigureInputField();
        Hide();
    }

    private void OnDestroy()
    {
        UnwireButtonListeners();
        UnwireInputField();
    }

    /// <summary>Shows the panel, sets the NPC label, clears input, and focuses the field.</summary>
    public void Show(string npcName)
    {
        if (npcLabel != null)
        {
            npcLabel.text = string.IsNullOrWhiteSpace(npcName) ? "TARGET" : npcName.ToUpperInvariant();
        }

        ClearInput();
        SetRootVisible(true);
        FocusInputField();
    }

    /// <summary>Hides the panel and clears session callbacks.</summary>
    public void Hide()
    {
        pendingSubmit = null;
        pendingCancel = null;
        SetRootVisible(false);
    }

    /// <summary>Current trimmed text in the question field (empty if missing).</summary>
    public string GetQuestionText()
    {
        return questionInputField != null ? questionInputField.text.Trim() : string.Empty;
    }

    /// <summary>Clears the question field without hiding the panel.</summary>
    public void ClearInput()
    {
        if (questionInputField != null)
        {
            questionInputField.text = string.Empty;
        }
    }

    /// <summary>
    /// Shows the panel and stores callbacks for this session only (used by <see cref="NPCBrain"/>).
    /// </summary>
    public void BeginSession(string npcDisplayName, Action<string> onSubmit, Action onCancel = null)
    {
        pendingSubmit = onSubmit;
        pendingCancel = onCancel;
        Show(npcDisplayName);
    }

    /// <summary>Hides without invoking submit/cancel callbacks.</summary>
    public void EndSessionSilently()
    {
        Hide();
    }

    private void WireButtonListeners()
    {
        if (submitButton != null)
        {
            submitButton.onClick.RemoveListener(HandleSubmitClicked);
            submitButton.onClick.AddListener(HandleSubmitClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(HandleCancelClicked);
            cancelButton.onClick.AddListener(HandleCancelClicked);
        }
    }

    private void UnwireButtonListeners()
    {
        if (submitButton != null)
        {
            submitButton.onClick.RemoveListener(HandleSubmitClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(HandleCancelClicked);
        }
    }

    private void ConfigureInputField()
    {
        if (questionInputField == null)
        {
            return;
        }

        questionInputField.lineType = TMP_InputField.LineType.SingleLine;
        questionInputField.onSubmit.RemoveListener(OnInputSubmit);
        questionInputField.onSubmit.AddListener(OnInputSubmit);
    }

    private void UnwireInputField()
    {
        if (questionInputField != null)
        {
            questionInputField.onSubmit.RemoveListener(OnInputSubmit);
        }
    }

    private void OnInputSubmit(string _)
    {
        HandleSubmitClicked();
    }

    private void HandleSubmitClicked()
    {
        string text = GetQuestionText();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Action<string> handler = pendingSubmit;
        Hide();
        handler?.Invoke(text);
    }

    private void HandleCancelClicked()
    {
        Action cancel = pendingCancel;
        Hide();
        cancel?.Invoke();
    }

    private void FocusInputField()
    {
        if (questionInputField == null)
        {
            return;
        }

        questionInputField.Select();
        questionInputField.ActivateInputField();
    }

    private void SetRootVisible(bool visible)
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

#if UNITY_EDITOR
    /// <summary>Editor-only: assign references from child names (called by custom inspector / builder).</summary>
    public void EditorAutoWireFromChildren()
    {
        Transform rootGroupTransform = FindDeepChild(transform, "RootGroup");
        if (rootGroupTransform == null)
        {
            return;
        }

        rootGroup = rootGroupTransform.GetComponent<CanvasGroup>();
        if (rootGroup == null)
        {
            rootGroup = rootGroupTransform.gameObject.AddComponent<CanvasGroup>();
        }

        npcLabel = FindDeepChild(rootGroupTransform, "NPCLabel")?.GetComponent<TextMeshProUGUI>();
        questionInputField = FindDeepChild(rootGroupTransform, "QuestionInputField")?.GetComponent<TMP_InputField>();
        submitButton = FindDeepChild(rootGroupTransform, "SubmitButton")?.GetComponent<Button>();
        cancelButton = FindDeepChild(rootGroupTransform, "CancelButton")?.GetComponent<Button>();
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindDeepChild(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
#endif
}
