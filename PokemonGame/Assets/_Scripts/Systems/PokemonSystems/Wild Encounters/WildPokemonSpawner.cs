using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WildPokemonSpawner : MonoBehaviour
{
    [SerializeField] private List<PokemonClass> _encounter;
    [SerializeField] private WildPokemon _wildPokemonPrefab;

    //-----------------------[ WEIGHTED RNG ]--------------------------

    [Header("Weighted RNG")]
    [SerializeField] private int[] table = {/* set in inspector hehe i hope this isn't bad design*/};
    [SerializeField] private int _totalWeight; //serialized for sight
    [SerializeField] private int _randomNumber; //serialized for sight

    private void RandomPokemon(){
        _totalWeight = 0;

        foreach( var num in table )
        {
            _totalWeight += num;
        }

        _randomNumber = Random.Range( 0, _totalWeight ) + 1;

        for( int i = 0; i < table.Length; i++ ){
            if( _randomNumber <= table[i] ){
                _wildPokemonPrefab.Pokemon = _encounter[i];
                return;
            }
            else{
                _randomNumber -= table[i];
            }
        }

    }

    //-------------------------------------------------------------------------------------------------------------//
    //-------------------------------------[ SPAWNER & DESPAWNER ]-------------------------------------------------//
    //-------------------------------------------------------------------------------------------------------------//
    
    [SerializeField] private float _wanderRange;
    [SerializeField] private int _spawnedPokemonAmnt; //--just to see how many are spawned in the inspector
    [SerializeField] private int _numberToSpawn;
    [SerializeField] private float _spawnDelay; //--time between spawns
    [SerializeField] private float _despawnTimer;
    [SerializeField] private float _respawnDelayMin; //--minimum time before a pokemon is generated after a despawn event happens
    [SerializeField] private float _respawnDelayMax; //--maximum time before a pokemon is generated after a despawn event happens
    [SerializeField] private List<Transform> _spawnLocations; //--list of empty game objects to use as the transform.position as spawn points
    private Transform _spawnLocation; //--assign randomly chosen spawnlocation to this for instantiate
    private bool _isSpawningPokemon;
    [SerializeField] private List<GameObject> _spawnedPokemonList;
    
    private void OnEnable(){
        //--Set Events
        WildPokemonEvents.OnPokeDespawned += DespawnTracker;
        // WildPokemonEvents.OnPlayerEncounter += RemoveFromSpawnLists;

        //--Initialize a new List/Queue instance unique to each spawner instance
        _spawnedPokemonList = new List<GameObject>();

        //--Add self to spawner list for widespread state control
        if( !WildPokemonSpawnerManager.Instance.SpawnerList.Contains( this ) ){
            WildPokemonSpawnerManager.Instance.SpawnerList.Add( this );
        }

        //--Start spawning pokemon, which should eventually be handled by the Spawner Manager and its State Machine
        StartCoroutine( SpawnPokemon() );
        StartCoroutine( DespawnTimer() );
    }

    private void OnDisable() {
        WildPokemonEvents.OnPokeDespawned -= DespawnTracker;
        // WildPokemonEvents.OnPlayerEncounter -= RemoveFromSpawnLists;
        
        StopAllCoroutines();

        DespawnPokemon();

        if( _spawnedPokemonAmnt != 0 )
            _spawnedPokemonAmnt = 0;
    }

    public IEnumerator SpawnPokemon(){
        //--Wait to start spawning lol
        yield return new WaitForSeconds( 2f );
        var gameStateStack = GameStateController.Instance.GameStateMachine.StateStack;

        if( gameStateStack.Peek() == FreeRoamState.Instance ){
            _isSpawningPokemon = true;
            WaitForSeconds Wait = new WaitForSeconds( _spawnDelay );

            while( _spawnedPokemonAmnt < _numberToSpawn ){
                var prevSpawnLocation = _spawnLocation;

                RandomPokemon();
                SpawnLocation();

                if( _spawnLocation == prevSpawnLocation ){
                    SpawnLocation();
                }

                if( _wildPokemonPrefab.Pokemon != null ){
                    GameObject pokemonToSpawn = Instantiate( _wildPokemonPrefab.gameObject, _spawnLocation.position, Quaternion.identity );
                    _spawnedPokemonAmnt++;
                    _spawnedPokemonList.Add( pokemonToSpawn );
                }

                yield return Wait;
            }

            _isSpawningPokemon = false;
        } else {
            yield break;
        }
    }

    //--This should probably have its own unique event that gets called when a wild pokemon's state changes
    //--the way it's set up right now should be fine, it gets called when the player collides with a mon, after all
    //--however, i wonder if i should have an "encountered" state, that gets swapped to when the player runs into a wild mon
    //--this encountered state would execute all of the necessary encounter code that currently gets set automatically
    //--it seems like a slightly excessive amount of organization, but it may prove useful to have to separated into its own
    //--state later down the line
    private void RemoveFromSpawnLists( WildPokemon wildPokemon ){
        if( _spawnedPokemonList.Contains( wildPokemon.gameObject ) ){
            _spawnedPokemonList.Remove( wildPokemon.gameObject );
            _spawnedPokemonAmnt--;
            Debug.Log( this + " has been removed encounter from the list by RemoveFromSpawnLists()" );
        }
    }
    
    //--This is a Force Despawn method that the spawner uses when it needs to immediately despawn all pokemon it's responsible for
    private void DespawnPokemon(){
        while( _spawnedPokemonList.Count > 0 ){
            Debug.Log( this + " despawned pokemon from DespawnPokemon()" );
            var wildMon = _spawnedPokemonList[0];
            _spawnedPokemonList.RemoveAt( 0 );
            wildMon.GetComponent<WildPokemon>()?.Despawn();
        }
    }

    //--This is called when a Wild Pokemon's OnPokeDespawned event is raised
    //--having 3 separate Despawn methods is clumsy and needs to be adjusted i think
    private void DespawnTracker( WildPokemon wildPokemon ){
        _spawnedPokemonAmnt--;
        if( !_isSpawningPokemon ){
            StartCoroutine( RespawnDelay() );
        }

        if( _spawnedPokemonList.Contains( wildPokemon.gameObject ) ){
            Debug.Log( this + " Removed pokemon from _spawnedPokemonList by DespawnTracker()" );
            _spawnedPokemonList.Remove( wildPokemon.gameObject );
        }
    }

    //--This is started immediately after SpawnPokemon starts. I'm wondering
    //--if i should give the timer back to each individual Wild Pokemon to keep track of themselves
    //--perhaps this is fine, though. having that many timers at once vs this one sloppy implementation
    //--is probably in the sloppy implementations favor in terms of performance
    public IEnumerator DespawnTimer(){
        yield return new WaitForSeconds( _despawnTimer );

        while( _spawnedPokemonList.Count >= 1 ){
            yield return new WaitForSeconds( _spawnDelay );

            if( _spawnedPokemonList[0] != null ){
                Debug.Log( this + " despawned pokemon from DespawnTimer()" );
                var wildMon = _spawnedPokemonList[0];
                wildMon.GetComponent<WildPokemon>()?.Despawn();
            }
        }
    }

    public IEnumerator RespawnDelay(){
        float respawnDelay = Random.Range( _respawnDelayMin, _respawnDelayMax );
        yield return new WaitForSeconds( respawnDelay );
        StartCoroutine( SpawnPokemon() );
    }

    private void SpawnLocation(){
        int rngLocation;
        for( int amountOfLocations = 0; amountOfLocations < _spawnLocations.Count; amountOfLocations++ ){
            rngLocation = Random.Range( 0, amountOfLocations );
            _spawnLocation = _spawnLocations[ rngLocation ];
        }
    }

}
