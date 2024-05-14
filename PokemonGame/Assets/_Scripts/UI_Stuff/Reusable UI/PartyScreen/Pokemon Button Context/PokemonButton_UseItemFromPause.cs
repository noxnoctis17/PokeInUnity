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
        Debug.Log( $"Use {_bagScreen.BagDisplay.ItemSelected.ItemSO.ItemName}, Count: {_bagScreen.BagDisplay.ItemSelected.ItemCount}, on {_pokemon.PokeSO.pName}" );
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

}
