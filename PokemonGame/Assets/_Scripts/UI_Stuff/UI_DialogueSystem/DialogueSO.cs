using UnityEngine;

[CreateAssetMenu( menuName = "Dialogue/DialogueObject")]
public class DialogueSO : ScriptableObject
{
    [SerializeField] [TextArea(5, 5)] private string[] _dialogue;
    public string[] Dialogue => _dialogue;
    [SerializeField] private Response[] _responses;
    public Response[] Responses => _responses;
    
    public bool HasResponses => Responses != null && Responses.Length > 0;
}
