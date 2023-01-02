using System;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static Action OnDialogueStarted;
    public static Action OnDialogueFinished;
    [SerializeField] private DialogueUI _dialogueUI;
    public DialogueUI DialogueUI => _dialogueUI;

    private void OnEnable( ){
        NPC_Base.OnNPCDialogueEvent += PlayDialogue;
        ResponseHandler.OnResponseChosen += PlayDialogue;
    }

    private void OnDisable( ){
        NPC_Base.OnNPCDialogueEvent -= PlayDialogue;
        ResponseHandler.OnResponseChosen -= PlayDialogue;
    }

    private void PlayDialogue( DialogueSO dialogueSO ){
        Debug.Log( "PlayDialogue" );
        GameStateTemp.GameState = GameState.Dialogue;
        GameStateTemp.OnGameStateChanged?.Invoke();
        OnDialogueStarted?.Invoke();
        _dialogueUI.ShowDialogue( dialogueSO );
    }

}
