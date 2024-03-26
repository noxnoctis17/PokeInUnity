using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class Spawner_FinishedState : State<WildPokemonSpawner>
{
    private WildPokemonSpawner _spawner;
    private int _spawnedAmount;

    private void OnEnable(){
        WildPokemonEvents.OnPokeDespawned += DespawnTracker;
    }

    private void OnDisable(){
        WildPokemonEvents.OnPokeDespawned -= DespawnTracker;
    }

    public override void EnterState( WildPokemonSpawner owner ){
        _spawner = owner;
        _spawnedAmount = _spawner.SpawnedPokemonAmnt;

        Debug.Log( "Enter Finished State" );
    }

    public override void ExitState(){

        Debug.Log( "Leaving Finished State" );
    }

    private void DespawnTracker( WildPokemon wildPokemon ){
        _spawnedAmount--;
        // _spawner.OnStateChanged?.Invoke( _spawner.SpawnState ); //--why is a despawned pokemon triggering a state change? this must've been temporary...
    }
}
