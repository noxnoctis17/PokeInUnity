using System;
using System.Collections;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;
    [SerializeField] private DialogueUI _dialogueUI;
    public DialogueUI DialogueUI => _dialogueUI;
    public Action<DialogueSO> OnDialogueEvent;
    public Action<string> OnStringDialogueEvent;
    public Action<string, bool> OnBattleDialogueEvent;
    public Action<bool> OnSystemDialogueComplete;
    public Action<DialogueSO> OnResponseChosen;
    public Action<DialogueResponseEvents> OnHasResponseEvents;
    public Action DialogueFinishedCallback;

    private void OnEnable( ){
        Instance = this;
        OnDialogueEvent             += PlayDialogue;
        // OnStringDialogueEvent       += PlayDialogue;
        OnResponseChosen            += ContinueDialogue;
        OnHasResponseEvents         += AddResponseEvents;
    }

    private void OnDisable( ){
        OnDialogueEvent             -= PlayDialogue;
        // OnStringDialogueEvent       -= PlayDialogue;
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

    public void PlaySystemMessage( string dialogue ){
        Debug.Log( "PlayDialogue()" );

        if( GameStateController.Instance.GameStateMachine.StateStack.Peek() != DialogueState.Instance ){
            Debug.Log( "pushed dialogue state ");
            GameStateController.Instance.GameStateMachine.Push( DialogueState.Instance );
        }

        StartCoroutine( _dialogueUI.StartSystemMessage( dialogue ) );
    }

    public IEnumerator PlaySystemMessageCoroutine( string dialogue ){
        Debug.Log( "PlayDialogue()" );

        if( GameStateController.Instance.GameStateMachine.StateStack.Peek() != DialogueState.Instance ){
            Debug.Log( "pushed dialogue state ");
            GameStateController.Instance.GameStateMachine.Push( DialogueState.Instance );
        }
        yield return _dialogueUI.StartSystemMessage( dialogue );
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
