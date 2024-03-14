using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleController : MonoBehaviour
{
    public static BattleController Instance;
    [SerializeField] private BattleSystem _battleSystem;

    private void OnEnable(){
        ConditionsDB.Init();
        WildPokemonEvents.OnPlayerEncounter += InitWildBattle;
    }

    private void Awake(){
        Instance = this;
    }

    private void InitWildBattle( WildPokemon encounteredMon ){
        //--Set Battle Type
        BattleType battleType = BattleType.WildBattle_1v1;

        //--Change Game State
        // GameStateController.Instance.GameStateMachine.Push( BattleState.Instance );
        GameStateController.Instance.GameStateMachine.ChangeState( BattleState.Instance );
        
        //--Assign Encountered Pokemon, Initialize Wild Battle
        _battleSystem.AssignWildPokemon( encounteredMon );
        _battleSystem.InitializeWildBattle( battleType );
    }

    public void InitTrainerBattle( PokemonParty trainerParty, BattleType battleType ){
        //--Change Game State
        // GameStateController.Instance.GameStateMachine.Push( BattleState.Instance );
        GameStateController.Instance.GameStateMachine.ChangeState( BattleState.Instance );

        //--Initialize Trainer Battle
        _battleSystem.InitializeTrainerSingles( trainerParty, battleType );
        
    }
}
