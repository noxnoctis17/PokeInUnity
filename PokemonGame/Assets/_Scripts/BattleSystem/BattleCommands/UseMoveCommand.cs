
//----------------------------------------------------------------------
//----------------------------Move Command------------------------------
//----------------------------------------------------------------------
using System;
using System.Collections;

public class UseMoveCommand : IBattleCommand
{
    private Move _move;
    private BattleUnit _attacker, _target;
    private BattleSystem _battleSystem;
    private int _commandPriority;
    private int _attackPriority;
    private int _unitAgility;
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
        // DeterminePriority();
    }

    public IEnumerator ExecuteBattleCommand(){
        yield return _battleSystem.PerformMoveCommand( _move, _attacker, _target );
    }

    private void DeterminePriority(){
        if( _move.MoveSO.MovePriority == MovePriority.one )
            _attackPriority = 1;
        else if( _move.MoveSO.MovePriority == MovePriority.two )
            _attackPriority = 2;
        
    }

}
