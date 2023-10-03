using System.Collections.Generic;
using UnityEngine;

public class WildPokemonManager : MonoBehaviour
{
    public static WildPokemonManager Instance { get; private set; }
    public List<WildPokemon> SpawnedPokemonList { get; private set; }

    private void OnEnable(){
        BattleSystem.OnBattleStarted += DisableColliders;
        BattleSystem.OnBattleStarted += PauseDespawnTimers;
        BattleSystem.OnBattleEnded += EnableColliders;
        BattleSystem.OnBattleEnded += RestartDespawnTimers;
    }

    private void OnDisable(){
        BattleSystem.OnBattleStarted -= DisableColliders;
        BattleSystem.OnBattleStarted -= PauseDespawnTimers;
        BattleSystem.OnBattleEnded -= EnableColliders;
        BattleSystem.OnBattleEnded -= RestartDespawnTimers;
    }

    private void Awake(){
        Instance = this;
        SpawnedPokemonList = new List<WildPokemon>();
    }

    //--We're doing all of this here instead of in a state because we don't actually
    //--want every single pokemon to have to enter a new state when a battle is started
    //--they should continue to do everything they would normally be doing, but without
    //--the despawn timer or colliders active
    
    private void PauseDespawnTimers(){
        foreach( var pokemon in SpawnedPokemonList ){
            StopCoroutine( pokemon?.DespawnTimer() );
        }
    }

    private void RestartDespawnTimers(){
        foreach( var pokemon in SpawnedPokemonList ){
            StartCoroutine( pokemon?.DespawnTimer() );
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

    private void OnGUI(){
        var style = new GUIStyle();
        style.fontSize = 24;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;

        GUILayout.BeginArea( new Rect( 0, 0, 500, 500 ) );
        GUILayout.Label( "WILD POKEMON STATE STACK", style );
        foreach( var pokemon in SpawnedPokemonList){
            foreach( var state in pokemon.WildPokemonStateMachine.StateStack ){
                GUILayout.Label( state.GetType().ToString(), style );
            }
        }
        GUILayout.EndArea();
    }
}
