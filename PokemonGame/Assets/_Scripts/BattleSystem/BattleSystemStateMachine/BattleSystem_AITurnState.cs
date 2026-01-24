using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class BattleSystem_AITurnState : State<BattleSystem>
{
    private BattleSystem _battleSystem;
    private List<BattleUnit> _availableAIUnit;
    private int _commands;

    public override void EnterState( BattleSystem owner )
    {
        _battleSystem = owner;
        _availableAIUnit = new();
        _commands = 0;
        _battleSystem.PlayerBattleMenu.OnPauseState?.Invoke();

        for( int i = 0; i < _battleSystem.EnemyUnits.Count; i++)
        {
            //--We increment the ai count based on the amount of ai units, but also,
            //--we need to make sure a unit doesn't have 0 hp in the case of doubles where there's only 1 enemy unit left.
            //--otherwise the OnAITurn event will add a command for a fainted unit
            if( _battleSystem.EnemyUnits[i].IsAI && _battleSystem.EnemyUnits[i].Pokemon.CurrentHP > 0 )
                _availableAIUnit.Add( _battleSystem.EnemyUnits[i] );
        }

        if( _battleSystem.BattleType == BattleType.AI_Singles || _battleSystem.BattleType == BattleType.AI_Doubles )
        {
            for( int i = 0; i < _battleSystem.PlayerUnits.Count; i++)
            {
                //--We increment the ai count based on the amount of ai units, but also,
                //--we need to make sure a unit doesn't have 0 hp in the case of doubles where there's only 1 enemy unit left.
                //--otherwise the OnAITurn event will add a command for a fainted unit
                if( _battleSystem.PlayerUnits[i].IsAI && _battleSystem.PlayerUnits[i].Pokemon.CurrentHP > 0 )
                    _availableAIUnit.Add( _battleSystem.PlayerUnits[i] );
            }
        }

        Debug.Log( $"Amount of AI Units: {_availableAIUnit.Count}" );

        foreach( var unit in _availableAIUnit )
        {
            unit.BattleAI.ChooseCommand();
            _commands++;
        }

        StartCoroutine( AwaitActionSelections() );
    }

    public override void ExitState()
    {
        _commands = 0;
        _availableAIUnit.Clear();
    }

    private IEnumerator AwaitActionSelections()
    {
        yield return new WaitUntil( () => _commands == _availableAIUnit.Count ); //--We do it this way because i plan on having battles where it's 2 vs 3 or more opponents, especially in boss battles
        //--All commands should have been added to the list, so now we determine command order and run turns.
        // _battleSystem.DetermineCommandOrder();
        _battleSystem.BeginCommandQueueState();
        yield return null;
    }

}
