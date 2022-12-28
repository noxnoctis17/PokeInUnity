using System;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    [SerializeField] private DialogueUI _dialogueUI;
    public DialogueUI DialogueUI => _dialogueUI;

    private void OnEnable( ){
        NPC_Base.OnNPCDialogueEvent += PlayDialogue;
    }

    private void OnDisable( ){
        NPC_Base.OnNPCDialogueEvent -= PlayDialogue;
    }

    private void PlayDialogue( DialogueSO dialogueSO ){
        _dialogueUI.ShowDialogue( dialogueSO );
    }

}
