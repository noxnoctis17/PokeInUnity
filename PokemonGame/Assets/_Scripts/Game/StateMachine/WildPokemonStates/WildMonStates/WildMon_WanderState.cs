using NoxNoctisDev.StateMachine;
using Pathfinding;
using UnityEngine;

public class WildMon_WanderState : State<WildPokemon>
{
	public static WildMon_WanderState Instance { get; private set; }
    private WildPokemon _wildPokemon;
    private PokemonSO _pokeSO;
    private WildPokemonWander _wander;

    private void Awake(){
        Instance = this;
    }
    
    public override void Enter( WildPokemon owner ){
        // Debug.Log( "Enter Wander State" );
        _wildPokemon = owner;
        _wander = _wildPokemon.WildPokemonWander;
        _pokeSO = _wildPokemon.Pokemon.PokeSO;

        //--Initialize Wandering by clearing any potentially odd destination or path, and then setting one
        if( _wander.AgentMon.hasPath ){
            _wander.AgentMon.SetPath( null );
        }
        SetWanderPoint();
        
    }

    public override void Execute(){
        WhenNearPlayer();

        if( _wander.AgentMon.reachedEndOfPath ){
            if( Random.Range( 1, 11 ) == 1 ){
                SetWanderPoint();
            }
            else{
                _wander.SetIdleState(); //--Set the state to idle
            }
        }
    }

    public override void Return(){
        // Debug.Log( "Return to Wander State" );
        if( _wander.AgentMon.hasPath && _wander.AgentMon.isStopped ){
            _wander.AgentMon.isStopped = false;
        }
        else{
            _wander.AgentMon.SetPath( null );
            SetWanderPoint();
        }
    }

    public override void Exit(){
        _wander.AgentMon.SetPath( null );
        // Debug.Log( "Exit Wander State" );
    }

    private void SetWanderPoint(){
        var mon = _wander.gameObject;
        var startNode = AstarPath.active.GetNearest( mon.transform.position, NNConstraint.Default ).node;
        var nodes = PathUtilities.BFS( startNode, 7 );
        var singleRandomPoint = PathUtilities.GetPointsOnNodes( nodes, 1 )[0];
        _wander.AgentMon.destination = singleRandomPoint;
    }

    private void WhenNearPlayer(){
        float discoverRange = 10;
        if( Vector3.Distance( transform.position, PlayerReferences.Instance.PlayerTransform.position ) < discoverRange ){
            switch( _pokeSO.WildType ){
                case WildType.Curious:

                    _wander.SetCuriousState();

                break;

                case WildType.Aggressive:

                    _wander.SetAggressiveState();

                break;
            }
        }
    }
}
