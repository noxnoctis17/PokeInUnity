using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameController : MonoBehaviour
{
    [SerializeField] private GameObject _player;
    [SerializeField] private BattleSystem _battleSystem;

    private void Awake(){
        ConditionsDB.Init();
        WildPokemon.OnWildPokemonCollided += InitWildBattle;
    }

    private void InitWildBattle(){
        GameStateTemp.GameState = GameState.Battle;
        StartCoroutine(StartWildBattle());
    }

    private IEnumerator StartWildBattle(){
        var playerParty = _player.GetComponent<PokemonParty>();
        var wildPokemon = WildPokemon.pokeSODelegate();
        yield return new WaitForSeconds(0.2f); //--battle start animation
        _battleSystem.StartWildBattle(playerParty, wildPokemon);

        yield return null;
    }
}
