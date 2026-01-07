using System;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    [SerializeField] private GameObject _gameboyDialogueBox;
    [SerializeField] private GameObject _dialogueBox;
    [SerializeField] private Image _dialogueBox_Trim;
    [SerializeField] private Image _dialogueBox_Inside;
    [SerializeField] private GameObject _leftSpeakerNameBox, _rightSpeakerNameBox;
    [SerializeField] private Image _leftSpeakerName_Inside, _rightSpeakerName_Inside;
    [SerializeField] private TMP_Text _leftSpeakerNameText_1, _rightSpeakerNameText_1;
    [SerializeField] private TextMeshProUGUI _gameboyText;
    [SerializeField] private TextMeshProUGUI _dialogueText;
    [SerializeField] private Image _leftPortrait_1, _leftPortrait_2, _rightPortrait_1, _rightPortrait_2;
    private PlayerInput _playerInput;
    private TypeText _typeText;
    private ResponseHandler _responseHandler;
    public IEnumerator ActiveDialogueCoroutine { get; private set; }

    private void Start()
    {
        _typeText = GetComponent<TypeText>();
        _responseHandler = GetComponent<ResponseHandler>();
        _playerInput = PlayerReferences.Instance.PlayerMovement.PlayerInput;
    }
    
    public void StartDialogue( DialogueSO dialogueSO )
    {
        ActiveDialogueCoroutine = StepThroughDialogue( dialogueSO );
        StartCoroutine( ActiveDialogueCoroutine );
    }

    public IEnumerator StartSystemMessage( string dialogue, bool skipButton = false )
    {
        ActiveDialogueCoroutine = StepThroughSystemMessage( dialogue, skipButton );
        yield return ActiveDialogueCoroutine;
    }

    public IEnumerator StartTrainerDialogue( string dialogue, Trainer trainer )
    {
        ActiveDialogueCoroutine = StepThroughTrainerDialogue( dialogue, trainer );
        yield return ActiveDialogueCoroutine;
    }

    public void AddResponseEvents( ResponseEvent[] responseEvents ){
        _responseHandler.AddResponseEvents( responseEvents );
    }

    private void SetDialogueBox( DialogueItem dialogueItem )
    {
        if( dialogueItem?.DialogueSpeaker == DialogueSpeaker.Speaker_1 )
        {
            _leftSpeakerNameText_1.text = dialogueItem.SpeakerName;
            _leftSpeakerName_Inside.color = dialogueItem.DialogueColor.Trim;
            _leftSpeakerNameBox.SetActive( true );
        }

        if( dialogueItem?.DialogueSpeaker == DialogueSpeaker.Speaker_2 )
        {
            _rightSpeakerNameText_1.text = dialogueItem.SpeakerName;
            _rightSpeakerName_Inside.color = dialogueItem.DialogueColor.Trim;
            _rightSpeakerNameBox.SetActive( true );
        }

        _dialogueBox_Trim.color = dialogueItem.DialogueColor.Trim;
        _dialogueBox_Inside.color = dialogueItem.DialogueColor.Inside;
        _dialogueBox.SetActive( true );
    }

    private void SetDialogueBox( Trainer trainer )
    {
        _leftSpeakerNameText_1.text = trainer.TrainerName;
        _leftSpeakerName_Inside.color = trainer.DialogueColor.Trim;
        _leftSpeakerNameBox.SetActive( true );
        
        if( trainer.TrainerSO.Portrait != null )
            _leftPortrait_1.sprite = trainer.TrainerSO.Portrait;

        _dialogueBox_Trim.color = trainer.DialogueColor.Trim;
        _dialogueBox_Inside.color = trainer.DialogueColor.Inside;

        _leftPortrait_1.gameObject.SetActive( true );
        _dialogueBox.SetActive( true );
    }

    private void SetDialogueBox()
    {
        _gameboyDialogueBox.SetActive( true );
    }

    private void SetDialoguePortraits( DialogueItem dialogueItem ){

        if( dialogueItem?.LeftPortrait_1 != null )
        {
            _leftPortrait_1.sprite = dialogueItem.LeftPortrait_1;
            _leftPortrait_1.gameObject.SetActive( true );
        }

        if( dialogueItem?.LeftPortrait_2 != null )
        {
            _leftPortrait_2.sprite = dialogueItem.LeftPortrait_2;
            _leftPortrait_2.gameObject.SetActive( true );
        }

        if( dialogueItem?.RightPortrait_1 != null )
        {
            _rightPortrait_1.sprite = dialogueItem.RightPortrait_1;
            _rightPortrait_1.gameObject.SetActive( true );
        }

        if( dialogueItem?.RightPortrait_2 != null )
        {
            _rightPortrait_2.sprite = dialogueItem.RightPortrait_2;
            _rightPortrait_2.gameObject.SetActive( true );
        }
    }

    private void ClearDialoguePortraits()
    {
        _leftPortrait_1.sprite = null;
        _leftPortrait_2.sprite = null;
        _rightPortrait_1.sprite = null;
        _rightPortrait_2.sprite = null;
        _leftPortrait_1?.gameObject.SetActive( false );
        _leftPortrait_2?.gameObject.SetActive( false );
        _rightPortrait_1?.gameObject.SetActive( false );
        _rightPortrait_2?.gameObject.SetActive( false );
    }

    private void ClearSpeakerNameText()
    {
        _leftSpeakerNameBox.SetActive( false );
        _rightSpeakerNameBox.SetActive( false );
        _leftSpeakerNameText_1.text = "";
        _rightSpeakerNameText_1.text = "";
    }
    
    private IEnumerator StepThroughDialogue( DialogueSO dialogueSO )
    {
        for( int i = 0; i < dialogueSO.DialogueItem.Length; i++ )
        {
            SetDialoguePortraits( dialogueSO.DialogueItem[i] );
            SetDialogueBox( dialogueSO.DialogueItem[i] );

            string dialogue = dialogueSO.DialogueItem[i].Dialogue;
            yield return RunTypingEffect( dialogue, _dialogueText );

            _dialogueText.text = dialogue;
            
            if( i == dialogueSO.DialogueItem.Length - 1 && dialogueSO.HasResponses )
                break;
            
            yield return null;
            yield return new WaitUntil( _playerInput.UI.Submit.WasReleasedThisFrame );
            
        }
        
        if( dialogueSO.HasResponses )
            _responseHandler.ShowResponses( dialogueSO.Responses );
        else
            CloseDialogueBox();

        yield return new WaitForSeconds( 0.25f );
    }

    private IEnumerator StepThroughSystemMessage( string dialogue, bool skipButton = false )
    {
        PlayerReferences.Instance.PlayerController.DisableUI();
        SetDialogueBox();

        yield return RunTypingEffect( dialogue, _gameboyText );

        PlayerReferences.Instance.PlayerController.EnableUI();
        yield return new WaitForEndOfFrame();
        
        if( !BattleSystem.BattleIsActive && !skipButton )
            yield return new WaitUntil( _playerInput.UI.Submit.WasReleasedThisFrame );
        else
            yield return new WaitForSeconds( 0.5f );

        CloseDialogueBox();
    }

    private IEnumerator StepThroughTrainerDialogue( string dialogue, Trainer trainer )
    {
        PlayerReferences.Instance.PlayerController.DisableUI();
        SetDialogueBox( trainer );

        yield return RunTypingEffect( dialogue, _dialogueText );
        yield return new WaitForSeconds( 0.5f );
        yield return new WaitForEndOfFrame();

        PlayerReferences.Instance.PlayerController.EnableUI();
        CloseDialogueBox();
    }

    private IEnumerator RunTypingEffect( string dialogue, TextMeshProUGUI textObj )
    {
        _typeText.RunDialogue( dialogue, textObj );

        while( _typeText.IsRunning ){
            yield return null;

            if( _playerInput.UI.Submit.WasReleasedThisFrame() )
                _typeText.StopDialogue();
        }
    }
    
    public void CloseDialogueBox()
    {
        if( GameStateController.Instance.CurrentStateEnum == GameStateController.GameStateEnum.DialogueState )
            GameStateController.Instance.GameStateMachine.Pop();

        StopAllCoroutines();
        
        _gameboyText.text = string.Empty;
        _dialogueText.text = string.Empty;

        ClearDialoguePortraits();
        ClearSpeakerNameText();

        _gameboyDialogueBox.SetActive( false );
        _dialogueBox.SetActive( false );

        //--Check and Execute the Callback on ResponsePicked, so that the Unity Event will trigger after Dialogue is completely over.
        //--This is especially important for trainer battles that have follow up dialogue before starting !!
        if ( DialogueManager.Instance.DialogueFinishedCallback != null ){
            Debug.Log( "DialogueFinishedCallback Raised" );
            DialogueManager.Instance.DialogueFinishedCallback.Invoke();
            DialogueManager.Instance.DialogueFinishedCallback = null;
        }
    }
}
