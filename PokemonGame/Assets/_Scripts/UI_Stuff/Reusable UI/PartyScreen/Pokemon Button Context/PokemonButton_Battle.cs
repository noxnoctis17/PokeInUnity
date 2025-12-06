using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PokemonButton_Battle : MonoBehaviour, IPokemonButtonContext
{
    private PartyDisplay _partyDisplay;
    private Pokemon _pokemon;
    private PokemonButton _pkmnButton;
    private PartyScreen_Battle _partyScreen;
    // private BattleDialogueBox _dialogueBox;
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
        // Debug.Log ("fainted select in button is: " + _isFaintedSelect );
        // _dialogueBox = BattleSystem.Instance.DialogueBox;
        Debug.Log( $"This pokemon is: {_pokemon.NickName}" );
        
        //--we're popping the state first because code is sequential
        //--all battle system stuff would get run to completion before we get to the end, where we'd finally pop the state
        //--we need to pop the state immediately. i have already made this change in MoveButton. I will likely need to do this
        //--for the pokeball (soon to be items) button.
        if( _pokemon.CurrentHP <= 0 ){
            DialogueManager.Instance.PlaySystemMessage( $"{_pokemon.NickName} is unable to fight!" );
            return;
        }

        for( int i = 0; i < _partyScreen.BattleSystem.PlayerUnits.Count; i++ )
        {
            if( _pokemon == _partyScreen.BattleSystem.PlayerUnits[i].Pokemon ){
                DialogueManager.Instance.PlaySystemMessage( "This Pokemon is already in battle!" );
                return;
            }
        }

        if( _partyScreen.BattleSystem.IsPokemonSelectedToShift( _pokemon ) )
        {
            DialogueManager.Instance.PlaySystemMessage( "This Pokemon is already selected!" );
            return;
        }

        //--We're popping the state here because the above two conditions need to keep the party screen open! --11/26/25
        //--I should really take a small dive into figuring out if popping a state before executing code is really actually doing anything or not
        _partyScreen.BattleMenu.StateMachine.Pop();

        if( _isFaintedSelect ){ //--If switch is caused by a faint, we don't add the command to the command queue
            BattleSystem.OnPlayerChoseNextPokemon?.Invoke();
            _partyScreen.BattleSystem.SetFaintedSwitchMon( _pokemon, _partyScreen.BattleSystem.SwitchUnitToPosition );
        }
        else{
            //--I NEED TO KNOW WHICH BATTLE UNIT IS CURRENTLY ADDING A COMMAND TO THE COMMAND QUEUE TO KNOW WHICH POSITION TO SWITCH TO
            // Debug.Log( $"The selected pokemon to switch in is: {_pokemon.NickName}, and returning is: { _partyScreen.BattleSystem.PlayerUnits[0].Pokemon.NickName}" );
            _partyScreen.BattleSystem.SetSwitchPokemonCommand( _pokemon, _partyScreen.BattleSystem.UnitInSelectionState );
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
            // StartCoroutine( _dialogueBox.TypeDialogue( "You need to select a Pokemon!" ) );
            DialogueManager.Instance.PlaySystemMessage( "You need to select a Pokemon!" );
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
        // Debug.Log( "SetFaintSelectTrue in button fired. should it have?" );
        _isFaintedSelect = true;
    }

    private void SetFaintSelectFalse(){
        // Debug.Log( "SetFaintSelectFalse in button fired. should it have?" );
        _isFaintedSelect = false;
    }
}
