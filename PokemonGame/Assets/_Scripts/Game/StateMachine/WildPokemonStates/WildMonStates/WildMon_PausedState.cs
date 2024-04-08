using NoxNoctisDev.StateMachine;
using UnityEngine;

public class WildMon_PausedState : State<WildPokemon>
{
    private WildPokemon _wildPokemon;
    
    public override void EnterState( WildPokemon owner ){
        Debug.Log( _wildPokemon + "Enter State: " + this );
        _wildPokemon = owner;
        _wildPokemon.PokeAnimator.OnAnimationStateChange?.Invoke( PokemonAnimator.AnimationState.Idle );

        if( _wildPokemon.AgentMon.hasPath ){
            _wildPokemon.AgentMon.isStopped = true;
        }
        else{
            _wildPokemon.AgentMon.SetPath( null );
        }
    }

    public override void ExitState(){

    }
}
