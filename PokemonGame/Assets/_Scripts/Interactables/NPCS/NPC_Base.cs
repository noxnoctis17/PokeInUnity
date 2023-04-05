using System;
using UnityEngine;

public class NPC_Base : MonoBehaviour, IInteractable
{
    [SerializeField] protected DialogueSO _dialogueSO;
    public DialogueSO DialogueSO => _dialogueSO;
    
    public virtual void Interact(){
        Debug.Log( $"You've Interacted With {this}" );

        foreach( DialogueResponseEvents responseEvents in GetComponents<DialogueResponseEvents>() ){
            if( responseEvents.DialogueSO == _dialogueSO ){
                DialogueManager.Instance.OnHasResponseEvents?.Invoke( responseEvents );
                break;
            }
        }

        DialogueManager.Instance.OnDialogueEvent?.Invoke( DialogueSO );

    }

    protected void UpdateDialogueObject( DialogueSO dialogueSO ){
        _dialogueSO = dialogueSO;
    }
}
