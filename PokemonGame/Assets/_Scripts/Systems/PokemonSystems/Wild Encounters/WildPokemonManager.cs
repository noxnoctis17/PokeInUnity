using System.Collections.Generic;
using UnityEngine;

public class WildPokemonManager : MonoBehaviour
{
    public static WildPokemonManager Instance { get; private set; }
    [SerializeField] public List<WildPokemon> SpawnedPokemonList;

    private void OnEnable(){
        BattleSystem.OnBattleStarted += DisableColliders;
        BattleSystem.OnBattleEnded += EnableColliders;
    }

    private void OnDisable(){
        BattleSystem.OnBattleStarted -= DisableColliders;
        BattleSystem.OnBattleEnded -= EnableColliders;
    }

    private void Awake(){
        if( Instance != null ){
            Destroy( Instance ); //--this is 100% going to come bite my asshole raw
            Instance = this;
        }
        else{
            Instance = this;
        }
    }

    private void EnableColliders(){
        foreach( var pokemon in SpawnedPokemonList ){
                StartCoroutine( pokemon?.CollisionDelay() );
        }
    }

    private void DisableColliders(){
        foreach( var pokemon in SpawnedPokemonList ){
            if( pokemon != null ){
                pokemon.BoxCollider.enabled = false;
            }
        }
    }
}
