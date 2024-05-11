using System.Collections;
using NoxNoctisDev.StateMachine;
using UnityEngine;
using UnityEngine.UI;

public class UseItemFromBagState : State<UI_PauseMenuStateMachine>
{
    private UI_PauseMenuStateMachine PauseMenuStateMachine;
    private Inventory _playerInventory;
    private Bag_PauseScreen _bagScreen;
    private PokemonButton[] _pkmnButtons;
    private Button _initialButton;
    public Button LastButton { get; private set; }
    
    public override void EnterState( UI_PauseMenuStateMachine owner ){
        //--State Machine
        PauseMenuStateMachine = owner;

        //--References
        _bagScreen = gameObject.GetComponent<Bag_PauseScreen>();
        _playerInventory = _bagScreen.PlayerInventory;
        _bagScreen.PartyDisplay.OnSubmittedButton += SetMemoryButton;

        _pkmnButtons = new PokemonButton[]{};
        _pkmnButtons = _bagScreen.PartyDisplay.PKMNButtons;

        _initialButton = _pkmnButtons[0].ThisButton;
        _bagScreen.PartyDisplay.SetPartyButtons_Interactable( true );
        StartCoroutine( SetInitialButton() );
    }

    public override void ReturnToState(){
        _bagScreen.PartyDisplay.OnSubmittedButton += SetMemoryButton;
        _bagScreen.PartyDisplay.SetPartyButtons_Interactable( true );
    }

    public override void PauseState(){
        _bagScreen.PartyDisplay.OnSubmittedButton -= SetMemoryButton;
        _bagScreen.PartyDisplay.SetPartyButtons_Interactable( false );
    }

    public override void ExitState(){
        Debug.Log( $"{this} ExitState()" );
        _bagScreen.PartyDisplay.OnSubmittedButton -= SetMemoryButton;
        _bagScreen.PartyDisplay.SetPartyButtons_Interactable( false );
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
