using System;
using UnityEngine;

public class NPC_Base : MonoBehaviour, IInteractable
{
    [SerializeField] private DialogueSO _dialogueSO;
    public DialogueSO DialogueSO => _dialogueSO;
    [SerializeField] private Sprite _dialoguePortrait;
    public Sprite DialoguePortrait => _dialoguePortrait;
    public static event Action<DialogueSO> OnNPCDialogueEvent;
    
    public void Interact(){
        Debug.Log( $"You've Interacted With {this}" );
        OnNPCDialogueEvent?.Invoke( DialogueSO );

    }
}
