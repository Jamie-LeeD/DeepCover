using UnityEngine;

/// <summary>
/// Authoring asset for reusable scripted conversations.
/// </summary>
[CreateAssetMenu(fileName = "DialogueAsset", menuName = "DeepCover/Dialogue Asset")]
public class DialogueAsset : ScriptableObject
{
    [SerializeField] private string speakerName = "NPC";
    [SerializeField] private DialogueLineData[] lines;

    public string SpeakerName => speakerName;
    public DialogueLineData[] Lines => lines;
}
