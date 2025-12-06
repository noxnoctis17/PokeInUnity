using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class BattleSystem_AITurnState : State<BattleSystem>
{
    private BattleSystem _battleSystem;
    private int _aiUnitCount;
    private int _commands;

    public override void EnterState( BattleSystem owner )
    {
        _battleSystem = owner;
        _aiUnitCount = 0;
        _commands = 0;
        _battleSystem.PlayerBattleMenu.OnPauseState?.Invoke();

        for( int i = 0; i < _battleSystem.EnemyUnits.Count; i++)
        {
            //--We increment the ai count based on the amount of ai units, but also,
            //--we need to make sure a unit doesn't have 0 hp in the case of doubles where there's only 1 enemy unit left.
            //--otherwise the OnAITurn event will add a command for a fainted unit
            if( _battleSystem.EnemyUnits[i].IsAI && _battleSystem.EnemyUnits[i].Pokemon.CurrentHP > 0 )
                _aiUnitCount++;
        }

        // Debug.Log( $"Amount of AI Units: {_aiUnitCount}" );

        for( int i = 0; i < _aiUnitCount; i++ )
        {
            if( _commands < _aiUnitCount )
            {
                // Debug.Log( $"Choosing a command for AI Unit {_battleSystem.EnemyUnits[i].Pokemon.NickName}" );
                _battleSystem.EnemyUnits[i].BattleAI.OnAITurn?.Invoke();
                _commands++;
                // Debug.Log( $"AI Command Count: {_commands}" );
            }
        }

        StartCoroutine( AwaitActionSelections() );
    }

    public override void ExitState()
    {
        _commands = 0;
        _aiUnitCount = 0;
    }

    private IEnumerator AwaitActionSelections()
    {
        yield return new WaitUntil( () => _commands == _aiUnitCount ); //--We do it this way because i plan on having battles where it's 2 vs 3 or more opponents, especially in boss battles
        //--All commands should have been added to the list, so now we determine command order and run turns.
        _battleSystem.DetermineCommandOrder();
        yield return null;
    }

}
