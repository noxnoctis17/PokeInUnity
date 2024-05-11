using System;
using UnityEngine;

public class PokemonButton_UseItem : MonoBehaviour, IPokemonButtonContext
{
    private PartyDisplay _partyDisplay;
    private Pokemon _pokemon;
    private PokemonButton _pkmnButton;
    private Bag_PauseScreen _bagScreen;

    public void Init( PartyDisplay partyScreen, PokemonButton button, IPartyScreen bagScreen ){
        _partyDisplay = partyScreen;
        _pkmnButton = button;
        _bagScreen = (Bag_PauseScreen)bagScreen;
        _pokemon = _pkmnButton.Pokemon;
    }

    public void ContextSubmit(){
        //--Using a local function is cool, and i get what it is, but i just barely am able to wrap my brain around creating this callback here, finally
        //-- so I'm keeping it as an Action for now until i understand and use callbacks and pass events/functions more. --05/10/24
        Action onItemUsed = () =>
        {
            if( _bagScreen.ItemSelected == null || _bagScreen.ItemSelected.ItemCount == 0 )
                _bagScreen.PauseMenuStateMachine.CloseCurrentMenu();
            else
                return;
        };

        Debug.Log( $"Use {_bagScreen.ItemSelected.ItemSO.ItemName}, Count: {_bagScreen.ItemSelected.ItemCount}, on {_pokemon.PokeSO.pName}" );
        PlayerReferences.Instance.PlayerInventory.UseItem( _bagScreen.ItemSelected, _pokemon, onItemUsed );
    }

    public void ContextSelected(){

    }

    public void ContextDeSelected(){

    }

    public void ContextCancel(){
        StartCoroutine( _pkmnButton.WaitForCloseAnims() );
    }

    public void CloseContextMenu(){
        Debug.Log( "ItemContext CloseContextMenu()" );
        _bagScreen.PauseMenuStateMachine.CloseCurrentMenu();
    }

}
