using System;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;
    public Action OnDialogueStarted;
    public Action OnDialogueFinished;
    public Action<DialogueSO> OnDialogueEvent;
    public Action<string> OnStringDialogueEvent;
    public Action<string, bool> OnBattleDialogueEvent;
    public Action<bool> OnSystemDialogueComplete;
    public Action<DialogueSO> OnResponseChosen;
    public Action<DialogueResponseEvents> OnHasResponseEvents;
    [SerializeField] private DialogueUI _dialogueUI;
    public Action DialogueFinishedCallback;

    public DialogueUI DialogueUI => _dialogueUI;

    private void OnEnable( ){
        Instance = this;
        OnDialogueEvent             += PlayDialogue;
        OnStringDialogueEvent       += PlayDialogue;
        OnBattleDialogueEvent       += PlayDialogue;
        OnResponseChosen            += ContinueDialogue;
        OnHasResponseEvents         += AddResponseEvents;
    }

    private void OnDisable( ){
        OnDialogueEvent             -= PlayDialogue;
        OnStringDialogueEvent       -= PlayDialogue;
        OnBattleDialogueEvent       -= PlayDialogue;
        OnResponseChosen            -= ContinueDialogue;
        OnHasResponseEvents         -= AddResponseEvents;
    }

    private void PlayDialogue( DialogueSO dialogueSO ){
        Debug.Log( "PlayDialogue()" );

        if( GameStateController.Instance.GameStateMachine.StateStack.Peek() != DialogueState.Instance ){
            Debug.Log( "pushed dialogue state ");
            GameStateController.Instance.GameStateMachine.Push( DialogueState.Instance );
        }
        Debug.Log( dialogueSO );
        _dialogueUI.StartDialogue( dialogueSO );
    }

    private void PlayDialogue( string dialogue ){
        Debug.Log( "PlayDialogue()" );

        if( GameStateController.Instance.GameStateMachine.StateStack.Peek() != DialogueState.Instance ){
            Debug.Log( "pushed dialogue state ");
            GameStateController.Instance.GameStateMachine.Push( DialogueState.Instance );
        }
        Debug.Log( dialogue );
        _dialogueUI.StartDialogue( dialogue );
    }

    private void PlayDialogue( string dialogue, bool battle ){
        Debug.Log( "PlayDialogue()" );

        if( GameStateController.Instance.GameStateMachine.StateStack.Peek() != DialogueState.Instance ){
            Debug.Log( "pushed dialogue state ");
            GameStateController.Instance.GameStateMachine.Push( DialogueState.Instance );
        }
        Debug.Log( dialogue );
        _dialogueUI.StartDialogue( dialogue, battle );
    }

    private void ContinueDialogue( DialogueSO dialogueSO ){
        Debug.Log( "ContinueDialogue()" );
        _dialogueUI.StartDialogue( dialogueSO );
    }

    private void AddResponseEvents( DialogueResponseEvents responseEvents ){
        _dialogueUI.AddResponseEvents( responseEvents.ResponseEvents );
    }

    public void SetDialogueFinishedCallback( Action callback ){
        // Save the callback function for later use
        DialogueFinishedCallback = callback;
    }

}
