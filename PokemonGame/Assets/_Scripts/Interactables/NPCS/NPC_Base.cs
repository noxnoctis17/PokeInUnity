using System;
using UnityEngine;

public class NPC_Base : MonoBehaviour, IInteractable
{
    [SerializeField] private DialogueSO _dialogueSO;
    public DialogueSO DialogueSO => _dialogueSO;
    
    public void Interact(){
        Debug.Log( $"You've Interacted With {this}" );

        foreach( DialogueResponseEvents responseEvents in GetComponents<DialogueResponseEvents>() ){
            if( responseEvents.DialogueSO == _dialogueSO ){
                DialogueManager.Instance.OnHasResponseEvents?.Invoke( responseEvents );
                break;
            }
        }

        DialogueManager.Instance.OnDialogueEvent?.Invoke( DialogueSO );

    }

    public void UpdateDialogueObject( DialogueSO dialogueSO ){
        _dialogueSO = dialogueSO;
    }
}
