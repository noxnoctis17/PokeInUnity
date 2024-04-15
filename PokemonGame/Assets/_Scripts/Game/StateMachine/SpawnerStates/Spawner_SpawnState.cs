using UnityEngine;
using NoxNoctisDev.StateMachine;
using System.Collections;

public class Spawner_SpawnState : State<WildPokemonSpawner>
{ 
    private WildPokemonSpawner _spawner;
    private int _addedAmount;
    private int _numberToSpawn;

    public override void EnterState( WildPokemonSpawner owner ){
        Debug.Log( "Enter Spawning State" );
        _spawner = owner;
        _numberToSpawn = _spawner.NumberToSpawn;

        StartCoroutine( SpawnPokemon() );
    }

    public override void ExitState(){
        StopAllCoroutines();
    }

    //--Checks the amount of pokemon currently spawned. if there are still pokemon that haven't been spawned (in the case where the spawner was paused
    //--and the spawning state was left) we "remember" that number as having been added, else it's 0
    private int CheckSpawnList(){
        if( _spawner.SpawnedPokemonAmnt > 0 )
            return _spawner.SpawnedPokemonAmnt;
        else
            return 0;
    }

    private IEnumerator SpawnPokemon(){
        WaitForSeconds delay = new( 1.5f );
        _addedAmount = CheckSpawnList();

        yield return delay;
        
        while( _spawner.SpawnedPokemonAmnt < _numberToSpawn ){
            var poolObj = _spawner.SpawnPool.Get();
            var position = _spawner.SpawnLocation();
            var pokemon = _spawner.RandomPokemon();

            Debug.Log( pokemon.PokeSO.pName + " will be spawned" );
            
            yield return null;

            poolObj.transform.position = position;
            poolObj.SetActive( true );
            poolObj.GetComponent<WildPokemon>().Init( _spawner, pokemon.PokeSO, pokemon.Level );
        
            _spawner.AddToSpawnedAmount();
            _addedAmount++;

            yield return delay;
        }

        _spawner.OnStateChanged?.Invoke( _spawner.FinishedState );
        yield return null;
    }

}
