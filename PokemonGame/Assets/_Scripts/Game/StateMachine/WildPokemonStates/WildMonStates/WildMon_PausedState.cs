using NoxNoctisDev.StateMachine;
using UnityEngine;

public class WildMon_PausedState : State<WildPokemon>
{
	public static WildMon_PausedState Instance { get; private set; }
    private WildPokemon _wildPokemon;
    private WildPokemonWander _wander;

    private void Awake(){
        Instance = this;
    }
    
    public override void Enter( WildPokemon owner ){
        Debug.Log( "Enter Paused State" );
        _wildPokemon = owner;
        _wander = _wildPokemon.WildPokemonWander;
        StopCoroutine( _wander.WanderIdle() );

        if( _wander.AgentMon.hasPath ){
            _wander.AgentMon.isStopped = true;
        }
        else{
            _wander.AgentMon.SetPath( null );
        }
    }

    public override void Exit(){
        Debug.Log( "Exit Paused State" );
    }
}
