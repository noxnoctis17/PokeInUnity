using UnityEngine;
using NoxNoctisDev.StateMachine;

public class WildMon_CuriousState : State<WildPokemon>
{
    private WildPokemon _wildPokemon;
    private float _stoppingDistance;
    
    public override void EnterState( WildPokemon owner ){
        // Debug.Log( _wildPokemon + "Enter State: " + this );
        _wildPokemon = owner;
        _wildPokemon.PokeAnimator.OnAnimationStateChange?.Invoke( PokeAnimationState.Walking );
        _wildPokemon.AgentMon.ResetPath();
        _stoppingDistance = _wildPokemon.AgentMon.stoppingDistance;
        _wildPokemon.AgentMon.stoppingDistance = 5f;
        _wildPokemon.AgentMon.destination = PlayerReferences.Instance.PlayerTransform.position;
    }

    public override void UpdateState(){

        float stopCuriousDistance = 11f;
        
        if( Vector3.Distance( transform.position, PlayerReferences.Instance.PlayerTransform.position ) > stopCuriousDistance ){
            _wildPokemon.AgentMon.stoppingDistance = 0.5f;
            _wildPokemon.OnPlayerTooFar?.Invoke( _wildPokemon.WanderState );
        }
    }

    public override void ExitState(){
        _wildPokemon.AgentMon.ResetPath();
        _wildPokemon.AgentMon.stoppingDistance = _stoppingDistance;
    }
}
