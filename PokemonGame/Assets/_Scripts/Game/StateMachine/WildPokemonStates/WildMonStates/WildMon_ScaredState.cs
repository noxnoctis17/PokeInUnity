using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class WildMon_ScaredState : State<WildPokemon>
{
	public static WildMon_ScaredState Instance { get; private set; }
    private WildPokemon _wildPokemon;
    private WildPokemonWander _wander;

    private void Awake(){
        Instance = this;
    }
    
    public override void Enter( WildPokemon owner ){
        Debug.Log( "Enter Scared State" );
        _wildPokemon = owner;
        _wander = _wildPokemon.WildPokemonWander;
    }

    public override void Execute(){

    }

    public override void Return(){
        
    }

    public override void Exit(){
        Debug.Log( "Exit Scared State" );
    }
}
