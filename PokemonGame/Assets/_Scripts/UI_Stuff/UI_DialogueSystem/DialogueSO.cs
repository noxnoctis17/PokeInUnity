using UnityEngine;

[CreateAssetMenu( menuName = "Dialogue/DialogueObject")]

public class DialogueSO : ScriptableObject
{
    // [SerializeField] [TextArea(5, 5)] private string[] _dialogue;
    [SerializeField] private DialogueItem[] _dialogueItem;
    public DialogueItem[] DialogueItem => _dialogueItem;

    [SerializeField] private Response[] _responses;
    public Response[] Responses => _responses;
    public bool HasResponses => Responses != null && Responses.Length > 0;

}

public enum DialogueSpeaker{
    None, Speaker_1, Speaker_2, Speaker_3, Speaker_4
}
