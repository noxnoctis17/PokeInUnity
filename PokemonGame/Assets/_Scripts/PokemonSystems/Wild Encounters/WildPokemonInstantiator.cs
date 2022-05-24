using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WildPokemonInstantiator : MonoBehaviour
{
    [SerializeField] private List<PokemonClass> _encounter;
    public WildPokemon wildPokemon;

    //-----------------------[ WEIGHTED RNG ]--------------------------

    [Header("Weighted RNG")]
    [SerializeField] private int[] table = {/* set in inspector hehe i hope this isn't bad design*/};
    [SerializeField] private int _totalWeight; //serialized for sight
    [SerializeField] private int _randomNumber; //serialized for sight

    public void RandomPokemon(){
        _totalWeight = 0;

        foreach(var num in table)
        {
            _totalWeight += num;
        }

        _randomNumber = Random.Range(0, _totalWeight) + 1;

        for(int i = 0; i < table.Length; i++){
            if(_randomNumber <= table[i]){
                wildPokemon.wildPokemon = _encounter[i];
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
    [SerializeField] private int _spawnedPokemon; //--just to see how many are spawned in the inspector
    [SerializeField] private int _numberToSpawn;
    [SerializeField] private float _spawnDelay; //--time between spawns
    [SerializeField] private float _respawnDelayMin; //--minimum time before a pokemon is generated after a despawn event happens
    [SerializeField] private float _respawnDelayMax; //--maximum time before a pokemon is generated after a despawn event happens
    [SerializeField] private List<Transform> _spawnLocations; //--list of empty game objects to use as the transform.position as spawn points
    private Transform _spawnLocation; //--assign randomly chosen spawnlocation to this for instantiate
    private int _wanderLength;
    private bool isSpawnCR;
    
    private void OnEnable(){
        WildPokemonSpawnEvents.OnPokeSpawn += SpawnTracker;
        WildPokemonSpawnEvents.OnPokeDespawn += DespawnTracker;

        if(!WildPokemonSpawnerManager.SpawnerList.Contains(this))
            WildPokemonSpawnerManager.SpawnerList.Add(this);

        StartCoroutine(SpawnPokemonCR());
    }

    private void OnDisable() {
        WildPokemonSpawnEvents.OnPokeSpawn -= SpawnTracker;
        WildPokemonSpawnEvents.OnPokeDespawn -= DespawnTracker;
        _spawnedPokemon = 0; //--Set the amount of spawned pokemon to 0 so the spawner is fully reset on re-enable

        StopAllCoroutines();
    }

    private void SpawnTracker(){
        _spawnedPokemon++;
    }

    private void DespawnTracker(){
        _spawnedPokemon--;
        if(!isSpawnCR)
        {
            StartCoroutine(RespawnDelay());
        }
    }

    private IEnumerator SpawnPokemonCR(){
        yield return new WaitForSeconds(5f); //--Wait to start spawning lol
        if(GameStateTemp.GameState == GameState.Overworld){
            isSpawnCR = true;
            WaitForSeconds Wait = new WaitForSeconds(_spawnDelay);

            while(_spawnedPokemon < _numberToSpawn){
                RandomPokemon();
                SpawnLocation();

                if(wildPokemon.wildPokemon != null){
                    GameObject pokemonToSpawn = Instantiate(wildPokemon.gameObject, _spawnLocation.position, Quaternion.identity);
                }

                yield return Wait;
            }

            isSpawnCR = false;
        } else {
            yield break;
        }
    }

    private IEnumerator RespawnDelay(){
        float respawnDelay = Random.Range(_respawnDelayMin, _respawnDelayMax);
        yield return new WaitForSeconds(respawnDelay);
        StartCoroutine(SpawnPokemonCR());
    }

    private void SpawnLocation(){
        int rngLocation;
        for(int amountOfLocations = 0; amountOfLocations < _spawnLocations.Count; amountOfLocations++){
            rngLocation = Random.Range(0, amountOfLocations);
            _spawnLocation = _spawnLocations[rngLocation];
        }
    }

    #if UNITY_EDITOR
    private void OnDrawGizmos(){
        Gizmos.DrawWireSphere(transform.position, _wanderRange);
    }
    #endif

}
