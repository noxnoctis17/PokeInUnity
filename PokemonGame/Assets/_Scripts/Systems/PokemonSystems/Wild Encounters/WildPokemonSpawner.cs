using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WildPokemonSpawner : MonoBehaviour
{
    [SerializeField] private List<PokemonClass> _encounter;
    [SerializeField] private WildPokemon _wildPokemonPrefab;

    //-------------------------------------------------------------------------------------------------------------//
    //--------------------------------------[ WEIGHTED RNG ]-------------------------------------------------------//
    //-------------------------------------------------------------------------------------------------------------//

    [Header("Weighted RNG")]
    [SerializeField] private int[] table = {/* set in inspector hehe i hope this isn't bad design*/};
    [SerializeField] private int _totalWeight; //serialized for sight
    [SerializeField] private int _randomNumber; //serialized for sight

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
    private bool _isRespawn;
    private bool _isSpawningPokemon;
    public List<GameObject> SpawnerPokemonList { get; private set; }
    
    private void OnEnable(){
        //--Set Events
        WildPokemonEvents.OnPokeDespawned += DespawnTracker;

        //--Initialize a new List/Queue instance unique to each spawner instance
        SpawnerPokemonList = new List<GameObject>();

        //--Add self to spawner list for widespread state control
        if( !WildPokemonSpawnerManager.Instance.SpawnerList.Contains( this ) ){
            WildPokemonSpawnerManager.Instance.SpawnerList.Add( this );
        }
    }

    private void OnDisable() {
        WildPokemonEvents.OnPokeDespawned -= DespawnTracker;
        
        StopAllCoroutines();
        DespawnPokemon();

        if( _spawnedPokemonAmnt != 0 )
            _spawnedPokemonAmnt = 0;
    }

    public IEnumerator SpawnPokemon(){
        //--Wait to start spawning lol
        yield return new WaitForSeconds( 2f );

        _isSpawningPokemon = true;
        WaitForSeconds Wait = new WaitForSeconds( _spawnDelay );

        if( _isRespawn ){
            yield return new WaitForSeconds( RespawnDelay() );
            _isRespawn = false;
        }

        while( _spawnedPokemonAmnt < _numberToSpawn ){
            var prevSpawnLocation = _spawnLocation;

            RandomPokemon();
            SpawnLocation();

            if( _spawnLocation == prevSpawnLocation ){
                SpawnLocation();
            }

            if( _wildPokemonPrefab.Pokemon != null ){
                GameObject pokemonToSpawn = Instantiate( _wildPokemonPrefab.gameObject, _spawnLocation.position, Quaternion.identity );
                pokemonToSpawn.GetComponent<WildPokemon>().SetBirthSpawner( this );
                _spawnedPokemonAmnt++;
                SpawnerPokemonList.Add( pokemonToSpawn );
            }

            yield return Wait;
        }

        _isSpawningPokemon = false;
    }
    
    private void DespawnPokemon(){
        while( SpawnerPokemonList?.Count > 0 ){
            // Debug.Log( this + " despawned pokemon from DespawnPokemon()" );
            var wildMon = SpawnerPokemonList[0];
            SpawnerPokemonList.RemoveAt( 0 );

            if( wildMon != null && !ReferenceEquals( wildMon, null ) ){
                var wildPokemon = wildMon.GetComponent<WildPokemon>();
                
                if( wildPokemon != null && !ReferenceEquals( wildPokemon, null ) ){
                    wildPokemon.Despawn();
                }
            }
        }
    }

    private void DespawnTracker( WildPokemon wildPokemon ){
        _spawnedPokemonAmnt--;
        _isRespawn = true;

        if( !_isSpawningPokemon ){
            StartCoroutine( SpawnPokemon() );
        }
    }

    private float RespawnDelay(){
        float respawnDelay = Random.Range( _respawnDelayMin, _respawnDelayMax );
        return respawnDelay;
    }

    private void SpawnLocation(){
        int rngLocation;
        for( int amountOfLocations = 0; amountOfLocations < _spawnLocations.Count; amountOfLocations++ ){
            rngLocation = Random.Range( 0, amountOfLocations );
            _spawnLocation = _spawnLocations[ rngLocation ];
        }
    }

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

}
