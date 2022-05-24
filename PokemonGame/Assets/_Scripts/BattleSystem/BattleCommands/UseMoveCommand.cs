
//----------------------------------------------------------------------
//----------------------------Move Command------------------------------
//----------------------------------------------------------------------
using System;
using System.Collections;

public class UseMoveCommand : IBattleCommand
{
    private MoveClass _move;
    private BattleUnit _attacker, _target;
    private BattleSystem _battleSystem;
    private int _priority;
    public int CommandPriority => _priority;
    private int _unitAgility;
    public int UnitAgility => _unitAgility;

    public UseMoveCommand(MoveClass move, BattleUnit attacker, BattleUnit target, BattleSystem battleSystem)
    {
        _move = move;
        _attacker = attacker;
        _target = target;
        _battleSystem = battleSystem;
        _unitAgility = attacker.Pokemon.Speed;
        _priority = DeterminePriority();
    }

    public IEnumerator ExecuteBattleCommand()
    {
        yield return _battleSystem.StartCoroutine(_battleSystem.PerformMoveCommand(_move, _attacker, _target));
    }

    private int DeterminePriority()
    {
        int movePriority;
        if(_move.moveBase.MovePriority == MovePriority.one)
            return movePriority = 5000;
        else if(_move.moveBase.MovePriority == MovePriority.two)
            return movePriority = 6000;
        else
            return movePriority = _attacker.Pokemon.Speed;
        
    }

}
