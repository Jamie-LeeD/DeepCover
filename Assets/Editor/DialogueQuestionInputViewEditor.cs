using UnityEditor;
using UnityEngine;

/// <summary>
/// Auto-wires <see cref="DialogueQuestionInputView"/> serialized fields from the standard child hierarchy.
/// </summary>
[CustomEditor(typeof(DialogueQuestionInputView))]
public class DialogueQuestionInputViewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Auto Wire Children", GUILayout.Height(28f)))
        {
            AutoWire((DialogueQuestionInputView)target);
        }

        EditorGUILayout.HelpBox(
            "Attach this script to the root GameObject named DialogueQuestionInputView.\n" +
            "Expected children: RootGroup → NPCLabel, QuestionInputField, SubmitButton, CancelButton.\n" +
            "Run Tools → Add Dialogue Question Input To Canvas (uses existing Canvas / EventSystem).",
            MessageType.Info);
    }

    private void OnEnable()
    {
        DialogueQuestionInputView view = (DialogueQuestionInputView)target;
        if (NeedsAutoWire(view))
        {
            AutoWire(view);
        }
    }

    private static bool NeedsAutoWire(DialogueQuestionInputView view)
    {
        SerializedObject so = new SerializedObject(view);
        return so.FindProperty("rootGroup").objectReferenceValue == null
               || so.FindProperty("questionInputField").objectReferenceValue == null;
    }

    internal static void AutoWire(DialogueQuestionInputView view)
    {
        if (view == null)
        {
            return;
        }

        Undo.RecordObject(view, "Auto Wire Dialogue Question Input");
        view.EditorAutoWireFromChildren();
        EditorUtility.SetDirty(view);
    }
}
