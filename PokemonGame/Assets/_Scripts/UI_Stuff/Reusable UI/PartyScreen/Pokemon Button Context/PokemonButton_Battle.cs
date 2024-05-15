using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PokemonButton_Battle : MonoBehaviour, IPokemonButtonContext
{
    private PartyDisplay _partyDisplay;
    private Pokemon _pokemon;
    private PokemonButton _pkmnButton;
    private PartyScreen_Battle _partyScreen;
    private BattleDialogueBox _dialogueBox;
    private bool _isFaintedSelect;

    public void Init( PartyDisplay partyScreen, PokemonButton button, IPartyScreen battleMenu ){
        _partyDisplay = partyScreen;
        _pkmnButton = button;
        _partyScreen = (PartyScreen_Battle)battleMenu;
        _pokemon = _pkmnButton.Pokemon;

        BattleSystem.OnPlayerPokemonFainted += SetFaintSelectTrue;
        BattleSystem.OnPlayerChoseNextPokemon += SetFaintSelectFalse;
    }

    public void ContextSubmit(){
        Debug.Log ("fainted select in button is: " + _isFaintedSelect );
        _dialogueBox = BattleSystem.Instance.DialogueBox;
        
        //--we're popping the state first because code is sequential
        //--all battle system stuff would get run to completion before we get to the end, where we'd finally pop the state
        //--we need to pop the state immediately. i have already made this change in MoveButton. I will likely need to do this
        //--for the pokeball (soon to be items) button.
        _partyScreen.BattleMenu.StateMachine.Pop();

        if( _pokemon.CurrentHP <= 0 ){
            Debug.Log( "You can't select a fainted Pokemon!" ); //message pop up eventually
            return;
        }

        if( _pokemon == _partyScreen.BattleSystem.PlayerUnit.Pokemon ){
            Debug.Log( "This Pokemon is already out!" ); //message pop up eventually
            return;
        }

        if( _isFaintedSelect ){ //--If switch is caused by a faint, we don't add the command to the command queue
            BattleSystem.OnPlayerChoseNextPokemon?.Invoke();
            _partyScreen.BattleSystem.SetFaintedSwitchMon( _pokemon );
        }
        else{
            _partyScreen.BattleSystem.SetSwitchPokemonCommand( _pokemon );
            BattleUIActions.OnCommandUsed?.Invoke();
            BattleUIActions.OnSubMenuClosed?.Invoke();
        }
        
        _partyScreen.SetMemoryButton( _pkmnButton.ThisButton );
    }

    public void ContextSelected(){
        
    }

    public void ContextDeSelected(){

    }

    public void ContextCancel(){
        if( _isFaintedSelect ){
            StartCoroutine( _dialogueBox.TypeDialogue( "You need to select a Pokemon!" ) );
        }
        else{
            _partyScreen.ClearMemoryButton();
            BattleUIActions.OnSubMenuClosed?.Invoke();
            StartCoroutine( _pkmnButton.WaitForCloseAnims() );
        }
    }

    public void CloseContextMenu(){
        _partyScreen.BattleMenu.StateMachine.Pop();
    }


    private void SetFaintSelectTrue(){
        Debug.Log( "SetFaintSelectTrue in button fired. should it have?" );
        _isFaintedSelect = true;
    }

    private void SetFaintSelectFalse(){
        Debug.Log( "SetFaintSelectFalse in button fired. should it have?" );
        _isFaintedSelect = false;
    }
}
