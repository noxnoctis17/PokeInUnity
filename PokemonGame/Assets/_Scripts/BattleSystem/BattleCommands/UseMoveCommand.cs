
//----------------------------------------------------------------------
//----------------------------Move Command------------------------------
//----------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UseMoveCommand : IBattleCommand
{
    private Move _move;
    private BattleUnit _attacker;
    private BattleUnit _singleTarget;
    private List<BattleUnit> _targets;
    private BattleSystem _battleSystem;
    private int _commandPriority;
    private int _attackPriority;
    private int _unitAgility;
    public BattleUnit SingleTarget => _singleTarget;
    public List<BattleUnit> Targets => _targets;
    public BattleUnit User => _attacker;
    public int CommandPriority => _commandPriority;
    public int AttackPriority => _attackPriority;
    public int UnitAgility => _unitAgility;
    public Move Move => _move;

    public UseMoveCommand( Move move, BattleUnit attacker, List<BattleUnit> targets, BattleSystem battleSystem )
    {
        _move = move;
        _attacker = attacker;
        _targets = targets;
        _battleSystem = battleSystem;
        _commandPriority = (int)CommandPriorityEnum.Attack;
        _attackPriority = GetPriority( attacker, move );
        _unitAgility = attacker.Pokemon.Speed;
        _unitAgility = Mathf.FloorToInt( attacker.Pokemon.Modify_SPD( _unitAgility, targets[0].Pokemon, move ) );

        if( targets.Count == 1 )
            _singleTarget = targets[0];
        else
            _singleTarget = null;
    }

    public IEnumerator ExecuteBattleCommand(){
        yield return _battleSystem.CommandCenter.PerformMoveCommand( _move, _attacker, _targets );
    }

    public void ChangeTarget( BattleUnit target )
    {
        _singleTarget = target;
    }

    private int GetPriority( BattleUnit attacker, Move move )
    {
        if( move.MoveSO.MoveCategory == MoveCategory.Status && attacker.Pokemon.Ability?.Name == "Prankster" )
        {
            attacker.SetFlagActive( UnitFlags.Prankster, true );
            return (int)move.Priority + 1;
        }
        else if( move.MoveSO.HasFlag( MoveFlags.Heal ) && attacker.Pokemon.Ability?.Name == "Triage" )
        {
            return (int)move.Priority + 3;
        }
        else
            return (int)move.Priority;
    }

}
