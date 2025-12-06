
//----------------------------------------------------------------------
//----------------------------Move Command------------------------------
//----------------------------------------------------------------------
using System;
using System.Collections;
using UnityEngine;

public class UseMoveCommand : IBattleCommand
{
    private Move _move;
    private BattleUnit _attacker, _target;
    private BattleSystem _battleSystem;
    private int _commandPriority;
    private int _attackPriority;
    private int _unitAgility;
    public BattleUnit Target => _target;
    public BattleUnit User => _attacker;
    public int CommandPriority => _commandPriority;
    public int AttackPriority => _attackPriority;
    public int UnitAgility => _unitAgility;

    public UseMoveCommand( Move move, BattleUnit attacker, BattleUnit target, BattleSystem battleSystem ){
        _move = move;
        _attacker = attacker;
        _target = target;
        _battleSystem = battleSystem;
        _commandPriority = (int)CommandPriorityEnum.Attack;
        _attackPriority = (int)_move.MoveSO.MovePriority;
        _unitAgility = attacker.Pokemon.Speed;
        _unitAgility = Mathf.FloorToInt( attacker.Pokemon.Modify_SPD( _unitAgility, target.Pokemon, move ) );
    }

    public IEnumerator ExecuteBattleCommand(){
        yield return _battleSystem.PerformMoveCommand( _move, _attacker, _target );
    }

    public void ChangeTarget( BattleUnit target )
    {
        _target = target;
    }

}
