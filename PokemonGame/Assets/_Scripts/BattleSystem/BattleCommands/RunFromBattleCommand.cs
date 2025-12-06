using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RunFromBattleCommand : IBattleCommand
{
    public BattleUnit User => _user;
    private int _commandPriority;
    public int CommandPriority => _commandPriority;
    public int AttackPriority => CommandPriority;
    public int UnitAgility => CommandPriority;
    private BattleSystem _battleSystem;
    private BattleUnit _user;

    public RunFromBattleCommand( BattleSystem battleSystem, BattleUnit user ){
        _battleSystem = battleSystem;
        _commandPriority = (int)CommandPriorityEnum.Run;
        _user = user;
    }

    public IEnumerator ExecuteBattleCommand(){
        yield return _battleSystem.PerformRunFromBattleCommand();
    }

    public void ChangeTarget( BattleUnit target )
    {
        //--This will definitely go unused
    }
}
