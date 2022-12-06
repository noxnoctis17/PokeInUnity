using System;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class DialogueUI : MonoBehaviour
{
    [SerializeField] private GameObject _dialogueBox;
    [SerializeField] private TMP_Text _dialogueText;
    [SerializeField] private DialogueSO _testDialogue;
    [SerializeField] private bool _interact;
    [SerializeField] private InputActionProperty _interactButton;
    private TypeText _typeText;
    private ResponseHandler _responseHandler;

    private void OnEnable(){
        _interactButton.action.performed += OnInteract;
        _interact = false;
        
    }

    private void OnDisable(){
        _interactButton.action.performed -= OnInteract;
        _interact = false;
        
    }

    private void Start(){
        _typeText = GetComponent<TypeText>();
        _responseHandler = GetComponent<ResponseHandler>();
        CloseDialogueBox();
        ShowDialogue( _testDialogue );
        
    }
    
    public void ShowDialogue( DialogueSO dialogueSO ){
        GameStateTemp.GameState = GameState.Dialogue;
        _dialogueBox.SetActive( true );
        StartCoroutine( StepThroughDialogue( dialogueSO ) );
        
    }
    
    private IEnumerator StepThroughDialogue( DialogueSO dialogueSO ){
        for( int i = 0; i < dialogueSO.Dialogue.Length; i++ ){
            string dialogue = dialogueSO.Dialogue[i];
            yield return _typeText.RunDialogue( dialogue, _dialogueText );
            
            if( i == dialogueSO.Dialogue.Length - 1 && dialogueSO.HasResponses )
                break;
            
            yield return new WaitUntil( () => _interact );
            _interact = false;
        }
        
        if( dialogueSO.HasResponses ){
            _responseHandler.ShowResponses( dialogueSO.Responses );
        } else{ 
            CloseDialogueBox();
        }
        
    }
    
    private void OnInteract( InputAction.CallbackContext context ){
        if( !_interact){
            _interact = true;
        }
    }
    
    private void CloseDialogueBox(){
        _dialogueBox.SetActive( false );
        _dialogueText.text = string.Empty;
        GameStateTemp.GameState = GameState.Overworld;
    }
}
