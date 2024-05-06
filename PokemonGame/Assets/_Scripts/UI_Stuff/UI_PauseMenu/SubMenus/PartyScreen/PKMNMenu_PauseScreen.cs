using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class PKMNMenu_PauseScreen : PKMNMenu_Base<UI_PauseMenuStateMachine>
{
    [SerializeField] private PartyScreen _partyScreen;
    [SerializeField] private PartyScreenContext _partyScreenContext;
    private UI_PauseMenuStateMachine _pauseMenuStateMachine;
    private Button _initialButton;
    public PartyScreen PartyScreen => _partyScreen;
    public Button LastButton { get; private set; }

    public override void EnterState( UI_PauseMenuStateMachine owner ){
        //--Set State Machine
        _pauseMenuStateMachine = owner;

        PKMNMenu_Events.OnPopPartyScreenState += PopScreenState;

        //--Initialize Party Screen
        _partyScreen.Init( _partyScreenContext );
        _partyScreen.SetParty( PlayerReferences.Instance.PlayerParty.PartyPokemon );

        //--Open Menu
        gameObject.SetActive( true );
        Debug.Log( "EnterState: " + this );

        //--Select Initial Button
        _initialButton = _partyScreen.PartyButton1;
        StartCoroutine( SetInitialButton() );
    }

    public override void ExitState(){
        PKMNMenu_Events.OnPopPartyScreenState -= PopScreenState;
        gameObject.SetActive( false );
    }

    private void PopScreenState(){
        _pauseMenuStateMachine.PauseMenuStateMachine.Pop();
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
