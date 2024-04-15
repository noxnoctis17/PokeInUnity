using NoxNoctisDev.StateMachine;
using Pathfinding;
using UnityEngine;

public class WildMon_WanderState : State<WildPokemon>
{
    private WildPokemon _wildPokemon;
    private PokemonSO _pokeSO;
    private bool _wander;
    
    public override void EnterState( WildPokemon owner ){
        // Debug.Log( _wildPokemon + "Enter State: " + this );
        _wildPokemon = owner;
        _pokeSO = _wildPokemon.Pokemon.PokeSO;
        _wildPokemon.PokeAnimator.OnAnimationStateChange?.Invoke( PokemonAnimator.AnimationState.Walking );
        _wander = true;
        
        //--Initialize Wandering by clearing any potentially odd/old destination or path, and then setting one
        if( _wildPokemon.AgentMon.hasPath ){
            _wildPokemon.AgentMon.SetPath( null );
        }

        SetWanderPoint();
    }

    public override void UpdateState(){
        WhenNearPlayer();

        if( _wander ){
            if( _wildPokemon.AgentMon.reachedEndOfPath ){
                if( Random.Range( 1, 11 ) == 1 ){
                    SetWanderPoint();
                }
                else{
                    _wildPokemon.WildPokemonStateMachine.OnQueueNextState?.Invoke( _wildPokemon.IdleState ); //--Set the state to idle
                }
            }
        }
        else{
            _wildPokemon.AgentMon.SetPath( null );
        }
    }

    public override void ExitState(){
        _wander = false;
        _wildPokemon.AgentMon.SetPath( null );
    }

    private void SetWanderPoint(){
        var mon = _wildPokemon.gameObject;
        var startNode = AstarPath.active.GetNearest( mon.transform.position, NNConstraint.Default ).node;
        var nodes = PathUtilities.BFS( startNode, 7 );
        var singleRandomPoint = PathUtilities.GetPointsOnNodes( nodes, 1 )[0];
        _wildPokemon.AgentMon.destination = singleRandomPoint;

        _wildPokemon.PokeAnimator.MoveX = _wildPokemon.AgentMon.desiredVelocity.x;
        _wildPokemon.PokeAnimator.MoveY = _wildPokemon.AgentMon.desiredVelocity.y;

    }

    private void WhenNearPlayer(){
        float discoverRange = 10;
        if( Vector3.Distance( transform.position, PlayerReferences.Instance.PlayerTransform.position ) < discoverRange ){
            switch( _pokeSO.WildType ){
                case WildType.Curious:

                    _wildPokemon.WildPokemonStateMachine.OnQueueNextState?.Invoke( _wildPokemon.CuriousState );

                break;

                case WildType.Aggressive:

                    _wildPokemon.WildPokemonStateMachine.OnQueueNextState?.Invoke( _wildPokemon.AggressiveState );

                break;
            }
        }
    }
}
