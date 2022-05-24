using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RunFromBattleCommand : IBattleCommand
{
    public int CommandPriority => 1000000;

    public int UnitAgility => CommandPriority;
    private BattleSystem _battleSystem;

    public RunFromBattleCommand(BattleSystem battleSystem)
    {
        _battleSystem = battleSystem;
    }

    public IEnumerator ExecuteBattleCommand()
    {
        yield return _battleSystem.PerformRunFromBattleCommand();
    }
}
