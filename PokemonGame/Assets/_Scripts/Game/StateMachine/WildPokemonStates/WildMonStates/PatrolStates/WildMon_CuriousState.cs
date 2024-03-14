using UnityEngine;
using NoxNoctisDev.StateMachine;

public class WildMon_CuriousState : State<WildPokemon>
{
	// public static WildMon_CuriousState Instance { get; private set; }
    private WildPokemon _wildPokemon;
    
    public override void EnterState( WildPokemon owner ){
        _wildPokemon = owner;
        _wildPokemon.PokeAnimator.OnAnimationStateChange?.Invoke( PokemonAnimator.AnimationState.Walking );
        _wildPokemon.AgentMon.SetPath( null );
        _wildPokemon.AgentMon.endReachedDistance = 5f;
        _wildPokemon.AgentMon.destination = PlayerReferences.Instance.PlayerTransform.position;
    }

    public override void UpdateState(){

        float stopCuriousDistance = 11f;
        
        if( Vector3.Distance( transform.position, PlayerReferences.Instance.PlayerTransform.position ) > stopCuriousDistance ){
            _wildPokemon.AgentMon.endReachedDistance = 0.5f;
            _wildPokemon.OnPlayerTooFar?.Invoke( _wildPokemon.WanderState );
        }
    }

    public override void ExitState(){
        _wildPokemon.AgentMon.SetPath( null );
    }
}
