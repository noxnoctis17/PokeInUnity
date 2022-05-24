using System;
using System.Collections.Generic;
using UnityEngine;

public class WildPokemonSpawnerManager : MonoBehaviour
{
    public static List<WildPokemonInstantiator> SpawnerList = new List<WildPokemonInstantiator>();
    private List<WildPokemonInstantiator> _disabledSpawnerList = new List<WildPokemonInstantiator>();
    public static WildPokemonSpawnerManager Instance { get; private set; }

    private void OnEnable(){
        BattleSystem.OnBattleEnded += EnableAllSpawners;
        BattleSystem.OnBattleStarted += DisableAllSpawners;
    }

    private void OnDisable(){
        BattleSystem.OnBattleEnded -= EnableAllSpawners;
        BattleSystem.OnBattleStarted -= DisableAllSpawners;
    }

    private void Awake(){
        if (Instance != null && Instance != this){ 
            Destroy(this); 
        } else { 
            Instance = this; 
        }
    }

    private void EnableAllSpawners(){
        foreach(WildPokemonInstantiator spawner in SpawnerList){
            spawner.gameObject.SetActive(true);
        }
    }

    private void DisableAllSpawners(){
        foreach(WildPokemonInstantiator spawner in SpawnerList){
            spawner.gameObject.SetActive(false);
        }
    }

    private void ClearSpawnerList(){
        foreach(WildPokemonInstantiator spawner in SpawnerList){
            SpawnerList.Remove(spawner);
        }
    }




}