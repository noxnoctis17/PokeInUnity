using System.Collections;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class Spawner_FinishedState : State<WildPokemonSpawner>
{
    private WildPokemonSpawner _spawner;
    private bool _isRespawning;

    public override void EnterState( WildPokemonSpawner owner ){
        Debug.Log( this + "Enter Finished State" );
        _spawner = owner;
    }

    public override void UpdateState(){
        if( _spawner.SpawnedPokemonAmnt < _spawner.NumberToSpawn && !_isRespawning )
            StartCoroutine( RespawnDelay() );
    }

    public override void ExitState(){
        Debug.Log( this + "Leaving Finished State" );
        _isRespawning = false;
    }

    private IEnumerator RespawnDelay(){
        Debug.Log( this + "respawn delay" );
        _isRespawning = true;
        yield return new WaitForSeconds( 3f );
        _spawner.OnStateChanged?.Invoke( _spawner.SpawnState );
    }

}
