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
    
    public void ContextSubmit()
    {
        _pokemonScreen.SetAsLastButton( _pkmnButton.ThisButton );

        if( _pokemonScreen.IsSwitchingPokemon )
        {
            if( _pokemon == _pokemonScreen.PokemonOptionMenu.ContextPokemon )
            {
                DialogueManager.Instance.PlaySystemMessage( $"You have to select a different Pokemon!", true );
                return;
            }
            else
                _pokemonScreen.PokemonOptionMenu.SetSwitchToPokemon( _pokemon );
        }
        else
        {
            _pokemonScreen.StateMachine.PushState( _pokemonScreen.PokemonOptionMenu );
            _pokemonScreen.PokemonOptionMenu.Init( _pokemonScreen, _partyDisplay, _pokemon, _pkmnButton );
        }
    }

    public void ContextSelected()
    {
        AudioController.Instance.PlaySFX( SoundEffect.ButtonSelect );
        int i = _partyDisplay.GetIndex( _pkmnButton );
        Vector3 rotate = new( 0f, 0f, 0f );
        _partyDisplay.MemberSlots[i].AnimateBall( rotate );
        _pokemonScreen.SetDisplayedPokemon( _pokemon );
    }

    public void ContextDeSelected()
    {
        int i = _partyDisplay.GetIndex( _pkmnButton );
        Vector3 rotate = new( 0f, 0f, 45f );
        _partyDisplay.MemberSlots[i].AnimateBall( rotate );
    }

    public void ContextCancel(){
        if( _pokemonScreen.IsSwitchingPokemon )
        {
            DialogueManager.Instance.PlaySystemMessage( $"You have to select a different Pokemon!" );
            return;
        }
        else
            StartCoroutine( _pkmnButton.WaitForCloseAnims() );
    }

    public void CloseContextMenu(){
        if( _pokemonScreen.StateMachine.StateMachine.CurrentState == _pokemonScreen )
            _pokemonScreen.StateMachine.PopState();
    }

    public void SetFollowerPokemon( FollowerPokemon follower )
    {
        _follower = follower;
    }
}
