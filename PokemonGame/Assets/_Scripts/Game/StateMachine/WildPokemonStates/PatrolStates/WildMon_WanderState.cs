using NoxNoctisDev.StateMachine;
using Pathfinding;
using UnityEngine;
using UnityEngine.AI;

public class WildMon_WanderState : State<WildPokemon>
{
    private WildPokemon _wildPokemon;
    private PokemonSO _pokeSO;
    private bool _wander;
    private float _radius;
    
    public override void EnterState( WildPokemon owner ){
        // Debug.Log( _wildPokemon + "Enter State: " + this );
        _wildPokemon = owner;
        _pokeSO = _wildPokemon.Pokemon.PokeSO;
        _wildPokemon.PokeAnimator.OnAnimationStateChange?.Invoke( PokeAnimationState.Walking );
        _wander = true;
        _radius = 5f;
        
        //--Initialize Wandering by clearing any potentially odd/old destination or path, and then setting one
        if( _wildPokemon.AgentMon.hasPath ){
            _wildPokemon.AgentMon.ResetPath();
        }

        SetWanderPoint();
    }

    public override void UpdateState(){
        WhenNearPlayer();
        
        if( _wander ){
            if( !_wildPokemon.AgentMon.hasPath ){
                if( Random.Range( 1, 11 ) == 1 )
                {
                    SetWanderPoint();
                }
                else
                {
                    _wildPokemon.WildPokemonStateMachine.OnQueueNextState?.Invoke( _wildPokemon.IdleState ); //--Set the state to idle
                }
            }
        }
        else{
            _wildPokemon.AgentMon.ResetPath();
        }

        _wildPokemon.PokeAnimator.MoveX = _wildPokemon.AgentMon.desiredVelocity.x;
        _wildPokemon.PokeAnimator.MoveY = _wildPokemon.AgentMon.desiredVelocity.y;
    }

    public override void ExitState(){
        _wander = false;
        _wildPokemon.AgentMon.ResetPath();
    }

    private void SetWanderPoint(){
        Vector3 random = Random.insideUnitSphere * _radius;
        random += transform.position;

        if ( NavMesh.SamplePosition( random, out NavMeshHit hit, _radius, NavMesh.AllAreas ) )
            _wildPokemon.AgentMon.destination = hit.position;
    }

    private void WhenNearPlayer(){
        float discoverRange = 10;

        if( Vector3.Distance( transform.position, PlayerReferences.Instance.PlayerTransform.position ) < discoverRange ){
            switch( _pokeSO.WildType ){
                case WildType.Curious:
                    if( Random.Range( 1, 11 ) == 1 )
                        _wildPokemon.WildPokemonStateMachine.OnQueueNextState?.Invoke( _wildPokemon.CuriousState );
                break;

                case WildType.Aggressive:
                    if( Random.Range( 1, 11 ) == 1 || Random.Range( 1, 11 ) == 2 || Random.Range( 1, 11 ) == 3 )
                        _wildPokemon.WildPokemonStateMachine.OnQueueNextState?.Invoke( _wildPokemon.AggressiveState );
                break;
            }
        }
    }
}
