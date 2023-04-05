using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleController : MonoBehaviour
{
    [SerializeField] private GameObject _player;
    [SerializeField] private BattleSystem _battleSystem;
    private PokemonParty _playerParty;

    private void OnEnable(){
        ConditionsDB.Init();
        WildPokemonEvents.OnPlayerEncounter += InitWildBattle;
    }

    private void InitWildBattle( WildPokemon encounteredMon ){
        //--Set Battle Type
        BattleType battleType = BattleType.WildBattle_1v1;

        //--Change Game State
        GameStateController.Instance.GameStateMachine.Push( BattleState.Instance );
        
        //--Assign Encountered Pokemon, Initialize Wild Battle
        _battleSystem.AssignWildPokemon( encounteredMon );
        _battleSystem.InitializeWildBattle( battleType );
    }

    private void InitTrainerBattle( PokemonParty trainerParty, BattleType battleType ){
        //--Set Battle Type
        
    }
}
