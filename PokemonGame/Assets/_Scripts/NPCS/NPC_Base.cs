using System;
using UnityEngine;
using UnityEngine.Events;

public class NPC_Base : MonoBehaviour, IInteractable
{
    [SerializeField] private DialogueSO _dialogueSO;
    public DialogueSO DialogueSO => _dialogueSO;
    public static event Action<DialogueSO> OnNPCDialogueEvent;
    // public UnityEvent<DialogueSO> OnNPCDialogueEvent;
    
    public void Interact(){
        Debug.Log( $"You've Interacted With {this}" );
        OnNPCDialogueEvent?.Invoke( DialogueSO );
    }
}
