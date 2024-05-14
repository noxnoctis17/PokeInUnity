using UnityEngine;

public class PokemonButton_PauseScreen : MonoBehaviour, IPokemonButtonContext
{
    private PartyDisplay _partyDisplay;
    private PartyScreen_Pause _pokemonScreen;
    private Pokemon _pokemon;
    private PokemonButton _pkmnButton;

    public void Init(  PartyDisplay partyScreen, PokemonButton button, IPartyScreen pokemonScreen ){
        _partyDisplay = partyScreen;
        _pkmnButton = button;
        _pokemonScreen = (PartyScreen_Pause)pokemonScreen;
        _pokemon = _pkmnButton.Pokemon;
        // _pkmnMenuPauseScreen = _pkmnMenu.GetComponent<PKMNMenu_PauseScreen>(); //really gotta clean this process up. make a middle ground inheritor for this shit or something
    }
    
    public void ContextSubmit(){
        Debug.Log( _pokemon );
        Debug.Log( _pokemon.PokeSO );
        Debug.Log( _pokemon.PokeSO.pName );
    }

    public void ContextSelected(){

    }

    public void ContextDeSelected(){

    }

    public void ContextCancel(){
        StartCoroutine( _pkmnButton.WaitForCloseAnims() );
    }

    public void CloseContextMenu(){
        _pokemonScreen.PauseMenuStateMachine.PopState();
    }

}
