using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using NoxNoctisDev.StateMachine;

public class PartyScreen_PauseScreen : State<UI_PauseMenuStateMachine>, IPartyScreen
{
    [SerializeField] private PartyDisplay _partyDisplay;
    // [SerializeField] private PartyScreenContext _partyScreenContext;
    public UI_PauseMenuStateMachine PauseMenuStateMachine { get; private set; }
    private Button _initialButton;
    public PartyDisplay PartyDisplay => _partyDisplay;
    public Button LastButton { get; private set; }

    public override void EnterState( UI_PauseMenuStateMachine owner ){
        //--Set State Machine
        PauseMenuStateMachine = owner;

        //--Open Menu
        gameObject.SetActive( true );
        Debug.Log( "EnterState: " + this );

        //--Select Initial Button
        _initialButton = _partyDisplay.PartyButton1;
        StartCoroutine( SetInitialButton() );
    }

    public override void ReturnToState(){
    }

    public override void PauseState(){
    }

    public override void ExitState(){
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
