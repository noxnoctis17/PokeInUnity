using System;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    [SerializeField] private GameObject _dialogueBox;
    [SerializeField] private TMP_Text _dialogueText;
    [SerializeField] private Image _leftPortrait, _rightPortrait;
    private PlayerInput _playerInput;
    private TypeText _typeText;
    private ResponseHandler _responseHandler;

    private void Start(){
        _typeText = GetComponent<TypeText>();
        _responseHandler = GetComponent<ResponseHandler>();
        _playerInput = PlayerMovement.PlayerInput;
        // CloseDialogueBox();
    }
    
    public void ShowDialogue( DialogueSO dialogueSO ){
        Debug.Log( "ShowDialogue" );
        _dialogueBox.SetActive( true );
        StartCoroutine( StepThroughDialogue( dialogueSO ) );
    }
    
    private IEnumerator StepThroughDialogue( DialogueSO dialogueSO ){
        for( int i = 0; i < dialogueSO.Dialogue.Length; i++ ){
            string dialogue = dialogueSO.Dialogue[i];
            yield return _typeText.RunDialogue( dialogue, _dialogueText );
            
            if( i == dialogueSO.Dialogue.Length - 1 && dialogueSO.HasResponses )
                break;
            
            yield return new WaitUntil( _playerInput.UI.Submit.WasReleasedThisFrame );
            
        }
        
        if( dialogueSO.HasResponses ){
            _responseHandler.ShowResponses( dialogueSO.Responses );
        }
        else{ 
            CloseDialogueBox();
        }
        
    }
    
    private void CloseDialogueBox(){
        Debug.Log( "CloseDialogueBox" );
        GameStateTemp.GameState = GameState.Overworld;
        GameStateTemp.OnGameStateChanged?.Invoke();
        DialogueManager.OnDialogueFinished?.Invoke();
        _dialogueText.text = string.Empty;
        _dialogueBox.SetActive( false );
    }
}
