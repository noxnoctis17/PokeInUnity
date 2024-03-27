using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UseItemCommand : IBattleCommand
{
    private int _commandPriority;
    public int CommandPriority => _commandPriority;
    public int AttackPriority => CommandPriority;
    public int UnitAgility => CommandPriority;
    private BattleSystem _battleSystem;

    public UseItemCommand( BattleSystem battleSystem ){
        _battleSystem = battleSystem;
        _commandPriority = (int)CommandPriorityEnum.Item;
    }

    public IEnumerator ExecuteBattleCommand(){
        yield return _battleSystem.ThrowPokeball();
    }
}
