using System;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    [SerializeField] private UnityEngine.GameObject _gameboyDialogueBox, _leftDialogueBox_1, _rightDialogueBox_1;
    [SerializeField] private UnityEngine.GameObject _speakerNameBox;
    [SerializeField] private TMP_Text _speakerNameText, _leftSpeakerNameText_1, _rightSpeakerNameText_1;
    [SerializeField] private TMP_Text _dialogueText;
    [SerializeField] private TMP_Text _leftSpeakerText_1, _leftSpeakerText_2, _rightSpeakerText_1, _rightSpeakerText_2;
    [SerializeField] private Image _leftPortrait_1, _leftPortrait_2, _rightPortrait_1, _rightPortrait_2;
    private PlayerInput _playerInput;
    private TypeText _typeText;
    private ResponseHandler _responseHandler;

    public IEnumerator ActiveDialogueCoroutine { get; private set; }

    private void Start(){
        _typeText = GetComponent<TypeText>();
        _responseHandler = GetComponent<ResponseHandler>();
        _playerInput = PlayerReferences.Instance.PlayerMovement.PlayerInput;
    }
    
    public void StartDialogue( DialogueSO dialogueSO ){
        Debug.Log( "StartDialogue" );
        ActiveDialogueCoroutine = StepThroughDialogue( dialogueSO );
        StartCoroutine( ActiveDialogueCoroutine );
    }

    public IEnumerator StartSystemMessage( string dialogue ){
        Debug.Log( "StartDialogue" );
        ActiveDialogueCoroutine = StepThroughSystemMessage( dialogue );
        yield return ActiveDialogueCoroutine;
    }

    public void AddResponseEvents( ResponseEvent[] responseEvents ){
        _responseHandler.AddResponseEvents( responseEvents );
    }

    private TMP_Text SetDialogueBox( DialogueItem dialogueItem ){
        if( dialogueItem?.DialogueSpeaker == DialogueSpeaker.Speaker_1 ){
            _rightDialogueBox_1.SetActive( false );
            _leftDialogueBox_1.SetActive( true );

            if( dialogueItem.SpeakerName != "" ){
            _leftSpeakerNameText_1.text = dialogueItem.SpeakerName;
            _leftSpeakerNameText_1.gameObject.SetActive( true );
            }

            return _leftSpeakerText_1;
        }

        if( dialogueItem?.DialogueSpeaker == DialogueSpeaker.Speaker_2 ){
            _leftDialogueBox_1.SetActive( false );
            _rightDialogueBox_1.SetActive( true );

            if( dialogueItem.SpeakerName != "" ){
            _rightSpeakerNameText_1.text = dialogueItem.SpeakerName;
            _rightSpeakerNameText_1.gameObject.SetActive( true );
            }

            return _rightSpeakerText_1;
        }
        else{
            _gameboyDialogueBox.SetActive( true );
            return _dialogueText;
        }
    }

    private TMP_Text SetDialogueBox(){
            _gameboyDialogueBox.SetActive( true );
            return _dialogueText;
    }

    private void SetDialoguePortraits( DialogueItem dialogueItem ){

        if( dialogueItem?.LeftPortrait_1 != null ){
            _leftPortrait_1.sprite = dialogueItem.LeftPortrait_1;
            _leftPortrait_1.gameObject.SetActive( true );
        }

        if( dialogueItem?.LeftPortrait_2 != null ){
            _leftPortrait_2.sprite = dialogueItem.LeftPortrait_2;
            _leftPortrait_2.gameObject.SetActive( true );
        }

        if( dialogueItem?.RightPortrait_1 != null ){
            _rightPortrait_1.sprite = dialogueItem.RightPortrait_1;
            _rightPortrait_1.gameObject.SetActive( true );
        }

        if( dialogueItem?.RightPortrait_2 != null ){
            _rightPortrait_2.sprite = dialogueItem.RightPortrait_2;
            _rightPortrait_2.gameObject.SetActive( true );
        }
    }

    private void ClearDialoguePortraits(){
        _leftPortrait_1.sprite = null;
        _leftPortrait_2.sprite = null;
        _rightPortrait_1.sprite = null;
        _rightPortrait_2.sprite = null;
        _leftPortrait_1?.gameObject.SetActive( false );
        _leftPortrait_2?.gameObject.SetActive( false );
        _rightPortrait_1?.gameObject.SetActive( false );
        _rightPortrait_2?.gameObject.SetActive( false );
    }

    private void ClearSpeakerNameText(){
        _speakerNameText.text = "";
        _leftSpeakerNameText_1.text = "";
        _rightSpeakerNameText_1.text = "";
    }
    
    private IEnumerator StepThroughDialogue( DialogueSO dialogueSO ){
        for( int i = 0; i < dialogueSO.DialogueItem.Length; i++ ){
            SetDialoguePortraits( dialogueSO.DialogueItem[i] );
            var currentSpeakerText = SetDialogueBox( dialogueSO.DialogueItem[i] );

            string dialogue = dialogueSO.DialogueItem[i].Dialogue;
            yield return RunTypingEffect( dialogue, currentSpeakerText );

            currentSpeakerText.text = dialogue;
            
            if( i == dialogueSO.DialogueItem.Length - 1 && dialogueSO.HasResponses )
                break;
            
            yield return null;
            yield return new WaitUntil( _playerInput.UI.Submit.WasReleasedThisFrame );
            
        }
        
        if( dialogueSO.HasResponses ){
            _responseHandler.ShowResponses( dialogueSO.Responses );
        }
        else{ 
            CloseDialogueBox();
        }

        yield return new WaitForSeconds( 0.25f );
        
    }

    private IEnumerator StepThroughSystemMessage( string dialogue ){
        PlayerReferences.Instance.PlayerController.DisableUI();
        var currentSpeakerText = SetDialogueBox();

        yield return RunTypingEffect( dialogue, currentSpeakerText );

        // currentSpeakerText.text = dialogue;
        // yield return null;
        PlayerReferences.Instance.PlayerController.EnableUI();
        yield return new WaitForEndOfFrame();
        yield return new WaitUntil( _playerInput.UI.Submit.WasReleasedThisFrame );
        CloseDialogueBox();
    }

    private IEnumerator RunTypingEffect( string dialogue, TMP_Text currentSpeakerText ){
        _typeText.RunDialogue( dialogue, currentSpeakerText );

        while( _typeText.IsRunning ){
            yield return null;

            if( _playerInput.UI.Submit.WasReleasedThisFrame() )
                _typeText.StopDialogue();
        }
    }
    
    public void CloseDialogueBox(){
        Debug.Log( "CloseDialogueBox()" );
        GameStateController.Instance.GameStateMachine.Pop();
        StopAllCoroutines();
        
        _dialogueText.text          = string.Empty;
        _leftSpeakerText_1.text     = string.Empty;
        _rightSpeakerText_1.text    = string.Empty;

        ClearDialoguePortraits();
        ClearSpeakerNameText();

        _gameboyDialogueBox.SetActive( false );
        _leftDialogueBox_1.SetActive( false );
        _rightDialogueBox_1.SetActive( false );

        //--Check and Execute the Callback on ResponsePicked, so that the Unity Event will trigger after Dialogue is completely over.
        //--This is especially important for trainer battles that have follow up dialogue before starting !!
        if ( DialogueManager.Instance.DialogueFinishedCallback != null ){
            Debug.Log( "DialogueFinishedCallback Raised" );
            DialogueManager.Instance.DialogueFinishedCallback.Invoke();
            DialogueManager.Instance.DialogueFinishedCallback = null;
        }
    }
}
