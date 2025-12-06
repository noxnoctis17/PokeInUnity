using System.Collections;
using UnityEngine;

public class PokemonButton_PauseScreen : MonoBehaviour, IPokemonButtonContext
{
    private PartyDisplay _partyDisplay;
    private PartyScreen_Pause _pokemonScreen;
    private Pokemon _pokemon;
    private PokemonButton _pkmnButton;
    [SerializeField] private FollowerPokemon _follower;

    public void Init(  PartyDisplay partyScreen, PokemonButton button, IPartyScreen pokemonScreen ){
        _partyDisplay = partyScreen;
        _pkmnButton = button;
        _pokemonScreen = (PartyScreen_Pause)pokemonScreen;
        _pokemon = _pkmnButton.Pokemon;
        _follower = PlayerReferences.Instance.FollowerPokemon;
    }
    
    public void ContextSubmit(){
        if( _pokemon.CanEvolveByLevelUp && _pokemon.CheckForEvolution() != null )
            StartCoroutine( TriggerEvolution() );
        else
        {
            _follower.SetFollowerPokemon( _pokemon );
        }
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

    private IEnumerator TriggerEvolution(){
        _partyDisplay.SetPartyButtons_Interactable( false );
        var evolution = _pokemon.CheckForEvolution();
        GameStateController.Instance.PushGameState( EvolutionManager.Instance );
        yield return EvolutionManager.Instance.Evolve( _pokemon, evolution );
        _partyDisplay.SetPartyButtons_Interactable( true );
        _pokemonScreen.LastButton.Select();
    }

}
