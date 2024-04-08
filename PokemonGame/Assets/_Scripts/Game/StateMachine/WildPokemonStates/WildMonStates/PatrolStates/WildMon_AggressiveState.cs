using NoxNoctisDev.StateMachine;
using UnityEngine;

public class WildMon_AggressiveState : State<WildPokemon>
{
	// public static WildMon_AggressiveState Instance { get; private set; }
    private WildPokemon _wildPokemon;
    private Vector3 _previousPosition;

    public override void EnterState( WildPokemon owner ){
        Debug.Log( _wildPokemon + "Enter State: " + this );
        _wildPokemon = owner;
        _wildPokemon.PokeAnimator.OnAnimationStateChange?.Invoke( PokemonAnimator.AnimationState.Walking );
        _wildPokemon.AgentMon.maxSpeed = 10f;
        _wildPokemon.AgentMon.maxAcceleration = 10f;
        _previousPosition = _wildPokemon.AgentMon.position;
        _wildPokemon.AgentMon.destination = PlayerReferences.Instance.PlayerTransform.position;
    }

    public override void UpdateState(){

        float stopAggressiveDistance = 15f;
        
        if( Vector3.Distance( transform.position, PlayerReferences.Instance.PlayerTransform.position ) > stopAggressiveDistance ){
            if( _wildPokemon.AgentMon.remainingDistance < 0.5f ){
                _wildPokemon.AgentMon.destination = _previousPosition;
            }

            if( Vector3.Distance( transform.position, _previousPosition ) < 0.5f ){
                _wildPokemon.AgentMon.maxSpeed = 3;
                _wildPokemon.AgentMon.maxAcceleration = 3f;
                _wildPokemon.OnPlayerTooFar?.Invoke( _wildPokemon.WanderState );
            }
        }
    }

    public override void ExitState(){
        _wildPokemon.AgentMon.SetPath( null );
    }
}
