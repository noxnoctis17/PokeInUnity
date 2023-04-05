using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NoxNoctisDev.StateMachine;

public class Spawner_SpawnState : State<WildPokemonSpawnerManager>
{
	public static Spawner_SpawnState Instance;
    private WildPokemonSpawnerManager _wildPokemonSpawnerManager;

    private void Awake(){
        Instance = this;
    }

    //--execute is what is called when a state is changed to. i wonder what the fuck enter is for lol
    //--exit is something we want the state to do after it is popped, likely something that it changed in Execute
    //--that can't quite be accounted for when returning to another state

    public override void Enter( WildPokemonSpawnerManager owner ){
        _wildPokemonSpawnerManager = owner;

        foreach( WildPokemonSpawner spawner in _wildPokemonSpawnerManager.SpawnerList ){
            Debug.Log( "Enter()_SpawnState" );
            StartCoroutine( spawner.SpawnPokemon() );
            StartCoroutine( spawner.DespawnTimer() );
            StartCoroutine( spawner.RespawnDelay() );
        }
    }

    public override void Exit(){
        Debug.Log( "SpawnState Exit() -- This Probably Shouldn't've Happened" );
    }
}
