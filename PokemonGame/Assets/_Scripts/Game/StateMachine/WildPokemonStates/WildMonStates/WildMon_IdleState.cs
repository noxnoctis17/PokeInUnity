using System.Collections;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class WildMon_IdleState : State<WildPokemon>
{
	public static WildMon_IdleState Instance { get; private set; }
    private WildPokemon _wildPokemon;
    private WildPokemonWander _wander;

    private void Awake(){
        Instance = this;
    }
    
    public override void Enter( WildPokemon owner ){
        // Debug.Log( "Enter Idle State" );
        _wildPokemon = owner;
        _wander = _wildPokemon.WildPokemonWander;

        _wander.AgentMon.SetPath( null );
        StartCoroutine( _wander.WanderIdle() );
    }

    public override void Return(){
        _wander.SetWanderState();
    }

    public override void Exit(){
        // Debug.Log( "Exit Idle State" );
    }
}
