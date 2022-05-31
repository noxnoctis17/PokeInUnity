using System.Collections;
using UnityEngine;
using System;

public class WildPokemonSpawnEvents : MonoBehaviour
{
    public static event Action OnPokeSpawn;
    public static event Action OnPokeDespawn;
    [SerializeField] private float _minDespawnTime;
    [SerializeField] private float _maxDespawnTime;
    private WildPokemon _wildPokemon;
    
    private void Start(){
        OnPokeSpawn?.Invoke();
        BattleSystem.OnBattleStarted += DestroyWildMonInstance;
        StartCoroutine(DespawnTimer());
        _wildPokemon = GetComponent<WildPokemon>();
    }

    private IEnumerator DespawnTimer(){
        float despawnDelay = UnityEngine.Random.Range(_minDespawnTime, _maxDespawnTime);
        yield return new WaitForSeconds(_maxDespawnTime);
        OnPokeDespawn?.Invoke();
        Destroy(this.gameObject);
    }

    private void DestroyWildMonInstance(){
        StopCoroutine(DespawnTimer());
        OnPokeDespawn?.Invoke();

        if( !_wildPokemon.Collided ){
            BattleSystem.OnBattleStarted -= DestroyWildMonInstance;
            Destroy(this.gameObject);
        } else {
            BattleSystem.OnBattleStarted -= DestroyWildMonInstance;
        }
    }
}
