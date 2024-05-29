using System;
using System.Collections;
using UnityEngine;

public class PokemonButton_UseItemFromPause : MonoBehaviour, IPokemonButtonContext
{
    private PartyDisplay _partyDisplay;
    private Pokemon _pokemon;
    private PokemonButton _pkmnButton;
    private BagScreen_Pause _bagScreen;

    public void Init( PartyDisplay partyScreen, PokemonButton button, IPartyScreen bagScreen ){
        _partyDisplay = partyScreen;
        _pkmnButton = button;
        _bagScreen = (BagScreen_Pause)bagScreen;
        _pokemon = _pkmnButton.Pokemon;
    }

    public void ContextSubmit(){
        Debug.Log( $"Use {_bagScreen.BagDisplay.ItemSelected.ItemSO.ItemName}, Count: {_bagScreen.BagDisplay.ItemSelected.ItemCount}, on {_pokemon.PokeSO.Name}" );
        if( _bagScreen.BagDisplay.ItemSelected.ItemSO.ItemCategory == ItemCategory.TM ){
            var tm = (TMItemSO)_bagScreen.BagDisplay.ItemSelected.ItemSO;
            StartCoroutine( UseTM( tm ) );
        }
        else
            StartCoroutine( UseItem() );
    }

    public void ContextSelected(){
        //--Arrow pointing and selected bounce animation start, from _partyDisplay
    }

    public void ContextDeSelected(){
        //--Arrow pointing and selected bounce animation cancel, from _partyDisplay. may not
    }

    public void ContextCancel(){
        _bagScreen.PauseMenuStateMachine.PopState();
    }

    public void CloseContextMenu(){
        Debug.Log( "ItemContext CloseContextMenu()" );
        _bagScreen.PauseMenuStateMachine.PopState();
    }

    private IEnumerator UseItem(){
        var itemUsed = PlayerReferences.Instance.PlayerInventory.UseItem( _bagScreen.BagDisplay.ItemSelected, _pokemon );

        if( itemUsed != null ){
            yield return DialogueManager.Instance.PlaySystemMessageCoroutine( itemUsed.UseText( _pokemon ) );

            if( _bagScreen.BagDisplay.ItemSelected == null || _bagScreen.BagDisplay.ItemSelected.ItemCount == 0 ){
                yield return new WaitForEndOfFrame();
                _bagScreen.PauseMenuStateMachine.PopState();
            }

        }
        else{
            yield return DialogueManager.Instance.PlaySystemMessageCoroutine( "It won't have any effect!" );
        }
        
        yield return null;
    }

    //--Check SV to see if using a TM keeps the player in the party field or forces them to use it again
    private IEnumerator UseTM( TMItemSO tm ){
        Debug.Log( "UseTM()" );
        if( !tm.CheckIfUsable( _pokemon ) ){
            Debug.Log( "CheckCanLearnMove" );
            yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"{_pokemon.PokeSO.Name} can't learn {tm.MoveSO.Name}! They're incompatible!" );
            yield break;
        }

        if( _pokemon.CheckHasMove( tm.MoveSO ) ){
            Debug.Log( "CheckHasMove" );
            yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"{_pokemon.PokeSO.Name} already knows {tm.MoveSO.Name}!" );
            yield break;
        }

        if( _pokemon.TryLearnMove( tm.MoveSO ) ){
            Debug.Log( "TryLearnMove" );
            yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"{_pokemon.PokeSO.Name} learned {tm.MoveSO.Name}!" );
        }
        else{

            bool moveLearnOver = false;
            bool learnedNewMove = false;

            Action<bool> onMoveLearnComplete = ( bool pokemonLearnedNewMove ) =>
            {
                //--This callback is retrieving two things:
                //--It's telling us that the move learn state is over, for the coroutine to continue
                //--and the bool it takes is receiving whether or not the move was actually learned
                moveLearnOver = true;
                learnedNewMove = pokemonLearnedNewMove;
            };

            yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"Which move will you set aside?" );

            _pokemon.TryReplaceMove_Pause( tm.MoveSO, _bagScreen.LearnMoveMenu, onMoveLearnComplete, _bagScreen );
            yield return new WaitUntil( () => moveLearnOver );

            if( learnedNewMove ){
                yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"{_pokemon.PokeSO.Name} learned {tm.MoveSO.Name}!" );
                yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"It was added to its Current Moves!" );
            }
            else{
                yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"{_pokemon.PokeSO.Name} learned {tm.MoveSO.Name}!" );
                yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"It was added to its Learned Moves!" );
            }
        }

        yield return new WaitForEndOfFrame();
    }

}
