using UnityEngine;
using NoxNoctisDev.StateMachine;
using System.Threading.Tasks;
using System.Collections;

public class Spawner_SpawnState : State<WildPokemonSpawner>
{ 
	// public static Spawner_SpawnState Instance;
    private WildPokemonSpawner _spawner;
    private int _spawnedAmount;
    private int _addedAmount;
    private int _numberToSpawn;
    private bool _isSpawning;

    private void Awake(){
        // Instance = this;
    }

    public override void EnterState( WildPokemonSpawner owner ){
        Debug.Log( "Enter Spawning State" );
        _spawner = owner;
        _numberToSpawn = _spawner.NumberToSpawn;
        CheckListForAdded();

        if( _spawner.SpawnerPokemonList.Count == 0 )
            StartCoroutine( SpawnPokemon() );
    }

    public override void ExitState(){
        StopCoroutine( SpawnPokemon() );
    }

    public override void UpdateState()
    {
        if( _spawnedAmount == 0 && !_isSpawning ){
            StartCoroutine( SpawnPokemon() );
        }
        
    }

    private void CheckListForAdded(){
        if( _spawner.PokemonToSpawn.Count > 0 )
            _addedAmount = _spawner.PokemonToSpawn.Count;
        else
            _addedAmount = 0;
    }

    //--the change i want to make to how i spawn pokemon
    //--is that i want to generate the full pool immediately and then add them to a list
    //--then go through the list and instantiate them
    //--the pool can then be cleared when necessary, but what might be cool
    //--is that it will allow me to potentially keep shiny pokemon in the pool
    //--so if one does get generated but doesn't spawn before you leave the area
    //--you don't actually miss out on it
    //--just an idea worth trying to rewrite some old code, at the very least, and see how i like it
    private IEnumerator SpawnPokemon(){
        _isSpawning = true;
        WaitForSeconds delay = new WaitForSeconds( 3f );
        yield return delay;

        while( _addedAmount < _numberToSpawn ){
            _spawner.OnAddPokemonToSpawn?.Invoke( _spawner.RandomPokemon() );
            _addedAmount++;
            yield return null;
        }

        if( _spawnedAmount != _numberToSpawn ){
            for( int i = _spawner.PokemonToSpawn.Count -1; i >= 0; i-- ){
                var prefab = _spawner.WildPokemonPrefab;
                var position = _spawner.SpawnLocation().position;
                var pokemon = _spawner.PokemonToSpawn[i];
                
                prefab.Pokemon = pokemon;
                prefab.PokeSO = pokemon.PokeSO;

                GameObject pokeObj = Instantiate( prefab.gameObject, position, Quaternion.identity );
                pokeObj.GetComponent<WildPokemon>().SetBirthSpawner( _spawner );
                // pokeObj.GetComponent<WildPokemon>().PokeSO = pokemon.PokeSO;
            
                _spawner.PokemonToSpawn.Remove( pokemon );
                _spawnedAmount++;
                yield return delay;
            }
        }

        _isSpawning = false;
        yield return null;
        _spawner.OnStateChanged?.Invoke( _spawner.FinishedState );
    }

}
