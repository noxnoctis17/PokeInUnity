using System;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;
    public Action OnDialogueStarted;
    public Action OnDialogueFinished;
    public Action<DialogueSO> OnDialogueEvent;
    public Action<DialogueSO> OnResponseChosen;
    public Action<DialogueResponseEvents> OnHasResponseEvents;
    [SerializeField] private DialogueUI _dialogueUI;
    public DialogueUI DialogueUI => _dialogueUI;

    private void OnEnable( ){
        Instance = this;
        OnDialogueEvent += PlayDialogue;
        OnResponseChosen += ContinueDialogue;
        OnHasResponseEvents += AddResponseEvents;
    }

    private void OnDisable( ){
        OnDialogueEvent -= PlayDialogue;
        OnResponseChosen -= ContinueDialogue;
        OnHasResponseEvents -= AddResponseEvents;
    }

    private void PlayDialogue( DialogueSO dialogueSO ){
        Debug.Log( "PlayDialogue" );

        if( GameStateController.Instance.GameStateMachine.StateStack.Peek() != DialogueState.Instance ){
            GameStateController.Instance.GameStateMachine?.Push( DialogueState.Instance );
        }
        _dialogueUI.StartDialogue( dialogueSO );
    }

    private void ContinueDialogue( DialogueSO dialogueSO ){
        Debug.Log( "ContinueDialogue" );
        _dialogueUI.StartDialogue( dialogueSO );
    }

    private void AddResponseEvents( DialogueResponseEvents responseEvents ){
        _dialogueUI.AddResponseEvents( responseEvents.ResponseEvents );
    }

}
