using NoxNoctisDev.StateMachine;
using UnityEngine;

public class WildMon_PausedState : State<WildPokemon>
{
	// public static WildMon_PausedState Instance { get; private set; }
    private WildPokemon _wildPokemon;
    
    public override void EnterState( WildPokemon owner ){
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
