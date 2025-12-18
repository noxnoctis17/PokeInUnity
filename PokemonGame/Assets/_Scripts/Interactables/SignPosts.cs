using System;
using UnityEngine;

public class SignPosts : MonoBehaviour, IInteractable
{
    [SerializeField] private DialogueSO _dialogueSO;

    public void Interact(){
        Debug.Log( $"You've Interacted With {this}" );
        DialogueManager.Instance.OnDialogueEvent?.Invoke( _dialogueSO );
    }
}
