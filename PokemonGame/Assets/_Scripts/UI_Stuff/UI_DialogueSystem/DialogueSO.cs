using UnityEngine;

[CreateAssetMenu( menuName = "Dialogue/DialogueObject")]
public class DialogueSO : ScriptableObject
{
    [SerializeField] private DialogueItem[] _dialogueItem;
    [SerializeField] private Response[] _responses;
    public DialogueItem[] DialogueItem => _dialogueItem;
    public Response[] Responses => _responses;
    public bool HasResponses => Responses != null && Responses.Length > 0;

}

public enum DialogueSpeaker{
    None, Speaker_1, Speaker_2, Speaker_3, Speaker_4, Gameboy,
}
