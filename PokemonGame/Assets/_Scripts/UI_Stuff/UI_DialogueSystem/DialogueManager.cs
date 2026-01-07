using System;
using System.Collections;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;
    [SerializeField] private DialogueUI _dialogueUI;
    public DialogueUI DialogueUI => _dialogueUI;
    public Action<DialogueSO> OnDialogueEvent;
    public Action<bool> OnSystemDialogueComplete;
    public Action<DialogueSO> OnResponseChosen;
    public Action<DialogueResponseEvents> OnHasResponseEvents;
    public Action DialogueFinishedCallback;

    private void OnEnable( ){
        Instance = this;
        OnDialogueEvent             += PlayDialogue;
        OnResponseChosen            += ContinueDialogue;
        OnHasResponseEvents         += AddResponseEvents;
    }

    private void OnDisable( ){
        OnDialogueEvent             -= PlayDialogue;
        OnResponseChosen            -= ContinueDialogue;
        OnHasResponseEvents         -= AddResponseEvents;
    }

    private void PlayDialogue( DialogueSO dialogueSO )
    {
        if( GameStateController.Instance.GameStateMachine.StateStack.Peek() != DialogueState.Instance )
            GameStateController.Instance.GameStateMachine.Push( DialogueState.Instance );

        _dialogueUI.StartDialogue( dialogueSO );
    }

    public void PlaySystemMessage( string dialogue, bool skipButton = false )
    {
        if( GameStateController.Instance.GameStateMachine.StateStack.Peek() != DialogueState.Instance )
            GameStateController.Instance.GameStateMachine.Push( DialogueState.Instance );

        StartCoroutine( _dialogueUI.StartSystemMessage( dialogue, skipButton ) );
    }

    public IEnumerator PlaySystemMessageCoroutine( string dialogue, bool skipButton = false )
    {
        if( GameStateController.Instance.GameStateMachine.StateStack.Peek() != DialogueState.Instance )
            GameStateController.Instance.GameStateMachine.Push( DialogueState.Instance );
        
        yield return _dialogueUI.StartSystemMessage( dialogue, skipButton );
    }

    public IEnumerator PlayTrainerDialogueCR( string dialogue, Trainer trainer )
    {
        if( GameStateController.Instance.GameStateMachine.StateStack.Peek() != DialogueState.Instance )
            GameStateController.Instance.GameStateMachine.Push( DialogueState.Instance );
        
        yield return _dialogueUI.StartTrainerDialogue( dialogue, trainer );
    }

    private void ContinueDialogue( DialogueSO dialogueSO ){
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
