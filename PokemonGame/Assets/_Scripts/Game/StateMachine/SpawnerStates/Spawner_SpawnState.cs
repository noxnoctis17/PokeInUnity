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

    //--Checks the spawner's list of pokemon to spawn. if there are still pokemon that haven't been spawned (in the case where the spawner was paused
    //--and the spawning state was left) we "remember" that number as having been added. if there are no pokemon remaining in the list, we set to 0
    private int CheckSpawnList(){
        if( _spawner.SpawnedPokemonAmnt > 0 )
            return _spawner.SpawnedPokemonAmnt;
        else
            return 0;
    }


    //--Moved CheckSpawnList() to execute here instead of in EnterState, because i believe it wasn't being called correctly when i needed it to for some reason.
    //--who knows tho haha
    //--This adds pokemon to the spawner's list (_addedAmount) according to how many pokemon that spawner should be spawning (_numberToSpawn)
    //--After _addedAmount is the same as _numberToSpawn (ie, the list is "full"), we instantiate the pokemon through the list backwards every 1.5 seconds
    //--Each time a pokemon is instantiated, we increase the SpawnedPokemonAmnt in the spawner. that value gets decreased every time a pokemon despawns either
    //--from its timer or from a battle ending from running from, fainting, or catching that wild pokemon. i wasn't actually checking the spawner's spawned amount
    //--until i added _spawner.AddToSpawnedAmount(), which i believe was one of the big issues being caused.
    private IEnumerator SpawnPokemon(){
        WaitForSeconds delay = new( 1.5f );
        _addedAmount = CheckSpawnList();
        int speciesIndex = 0;
        // GameObject poolObj;
        // Vector3 position;
        // PokemonClass pokemon;

        yield return delay;

        // while( _addedAmount < _numberToSpawn ){
        //     _spawner.OnAddPokemonToSpawn?.Invoke( _spawner.RandomPokemon() );
        //     _addedAmount++;
        // }
        
        //--currently doesn't properly reassign new information to the pooled object when it's used after it's first despawn
        while( _spawner.SpawnedPokemonAmnt < _numberToSpawn ){
            // poolObj = _spawner.SpawnPool.Get();
            // position = _spawner.SpawnLocation().position;
            // pokemon = _spawner.SpeciesToSpawnList[speciesIndex];
            var poolObj = _spawner.SpawnPool.Get();
            var position = _spawner.SpawnLocation();
            var pokemon = _spawner.RandomPokemon();
            // var pokemon = _spawner.SpeciesToSpawnList[speciesIndex];

            Debug.Log( pokemon.PokeSO.pName + " will be spawned" );
            

            yield return null;

            poolObj.transform.position = position;
            poolObj.SetActive( true );
            poolObj.GetComponent<WildPokemon>().Init( _spawner, pokemon.PokeSO, pokemon.Level );
        
            _spawner.AddToSpawnedAmount();
            Debug.Log( "speciesIndex: " + speciesIndex );
            speciesIndex++;
            _addedAmount++;

            yield return delay;
        }

        // while( _spawner.SpawnedPokemonAmnt < _numberToSpawn ){
        //     for( int i = _spawner.SpawnPool.Count - 1; i >= 0; i-- ){
        //         var prefab = _spawner.WildPokemonPrefab;
        //         var position = _spawner.SpawnLocation().position;
        //         var pokemon = _spawner.SpawnPool[i];
                
        //         prefab.Pokemon = pokemon;
        //         prefab.PokeSO = pokemon.PokeSO;

        //         GameObject pokeObj = Instantiate( prefab.gameObject, position, Quaternion.identity );
        //         pokeObj.GetComponent<WildPokemon>().SetBirthSpawner( _spawner );
            
        //         _spawner.SpawnPool.Remove( pokemon );
        //         _spawner.AddToSpawnedAmount();
        //         yield return delay;
        //     }
        // }

        _spawner.OnStateChanged?.Invoke( _spawner.FinishedState );
        yield return null;
    }

}
