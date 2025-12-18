using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using NoxNoctisDev.StateMachine;
using DG.Tweening;

public class PartyScreen_Pause : State<UI_PauseMenuStateMachine>, IPartyScreen
{
    [SerializeField] private PartyDisplay _partyDisplay;
    public UI_PauseMenuStateMachine PauseMenuStateMachine { get; private set; }
    private Button _initialButton;
    public PartyDisplay PartyDisplay => _partyDisplay;
    public Button LastButton { get; private set; }

    public override void EnterState( UI_PauseMenuStateMachine owner ){
        Debug.Log( "EnterState: " + this );
        //--Set State Machine
        PauseMenuStateMachine = owner;

        //-Set Off-Screen if it wasn't, for slide-in Animation
        transform.DOLocalMoveX( -650f, 0f );

        //--Open Menu
        gameObject.SetActive( true );
        transform.DOLocalMoveX( 0f, 0.25f );

        //--Select Initial Button
        _initialButton = _partyDisplay.PartyButton1;
        StartCoroutine( SetInitialButton() );
    }

    public override void ReturnToState(){
    }

    public override void PauseState(){
    }

    public override void ExitState(){
        StartCoroutine( ExitMenuAnim() );
    }

    private IEnumerator ExitMenuAnim(){
        //-Set Off-Screen if it wasn't, for slide-in Animation
        yield return transform.DOLocalMoveX( -650f, 0.25f );
        gameObject.SetActive( false );
    }

    private IEnumerator SetInitialButton(){
        yield return new WaitForSeconds( 0.15f );

        if( LastButton != null )
            SelectMemoryButton();
        else{
            SetMemoryButton( _initialButton );
        }
    }

    public void SetMemoryButton( Button lastButton ){
        LastButton = lastButton;
        SelectMemoryButton();
    }

    private void SelectMemoryButton(){
        LastButton.Select();
    }

    public void ClearMemoryButton(){
        LastButton = null;
        _initialButton.Select();
    }

}
