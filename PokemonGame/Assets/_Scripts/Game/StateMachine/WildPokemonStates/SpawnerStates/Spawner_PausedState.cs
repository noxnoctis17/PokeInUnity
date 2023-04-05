using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NoxNoctisDev.StateMachine;

public class Spawner_PausedState : State<WildPokemonSpawnerManager>
{
	public static Spawner_PausedState Instance;
    private WildPokemonSpawnerManager _wildPokemonSpawnerManager;

    private void Awake(){
        Instance = this;
    }

    public override void Enter( WildPokemonSpawnerManager owner ){
        _wildPokemonSpawnerManager = owner;

        foreach( WildPokemonSpawner spawner in _wildPokemonSpawnerManager.SpawnerList ){
            Debug.Log( "PauseSpawner()" );
            StopCoroutine( spawner.SpawnPokemon() );
            StopCoroutine( spawner.DespawnTimer() );
            StopCoroutine( spawner.RespawnDelay() );
        }
    }

    public override void Exit(){
        foreach( WildPokemonSpawner spawner in _wildPokemonSpawnerManager.SpawnerList ){
            Debug.Log( "ResumeSpawner()" );
            StartCoroutine( spawner.SpawnPokemon() );
            StartCoroutine( spawner.DespawnTimer() );
            StartCoroutine( spawner.RespawnDelay() );
        }
    }
}
