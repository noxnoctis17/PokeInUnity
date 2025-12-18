using UnityEngine;

[System.Serializable]
public class Response
{
    [SerializeField] private string _responseText;
    [SerializeField] private DialogueSO _dialogueSO;
    public string ResponseText => _responseText;
    public DialogueSO DialogueSO => _dialogueSO;
}
