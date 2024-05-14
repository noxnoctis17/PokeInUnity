using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PokemonButton_UseItemInBattle : MonoBehaviour, IPokemonButtonContext
{
    private PartyDisplay _partyDisplay;
    private Pokemon _pokemon;
    private PokemonButton _pkmnButton;
    private BagScreen_Battle _bagScreen;

    public void Init( PartyDisplay partyScreen, PokemonButton button, IPartyScreen bagScreen ){
        _partyDisplay = partyScreen;
        _pkmnButton = button;
        _bagScreen = (BagScreen_Battle)bagScreen;
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
        //--Arrow pointing and selected bounce animation cancel, from _partyDisplay. may not need
    }

    public void ContextCancel(){
        _bagScreen.BattleMenu.PopState();
    }

    public void CloseContextMenu(){

    }

    private IEnumerator UseItem(){
        var itemUsable = PlayerReferences.Instance.PlayerInventory.CheckIfItemUsable( _bagScreen.BagDisplay.ItemSelected, _pokemon );

        if( itemUsable ){
            _bagScreen.OnItemCommand?.Invoke( _pokemon, _bagScreen.BagDisplay.ItemSelected );
        }
        else
            yield return DialogueManager.Instance.PlaySystemMessageCoroutine( "It won't have any effect!" );
        
        yield return null;
    }

}
