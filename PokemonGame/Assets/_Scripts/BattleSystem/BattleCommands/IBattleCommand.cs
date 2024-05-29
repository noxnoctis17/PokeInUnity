
//-----------------------------------------------------------------------------
//-------------------------Battle Command Interface----------------------------
//-----------------------------------------------------------------------------
using System.Collections;

public interface IBattleCommand
{
    public int CommandPriority { get; }
    public int AttackPriority { get; }
    public int UnitAgility { get; }

    public IEnumerator ExecuteBattleCommand();

}

public enum CommandPriorityEnum{
    Attack,
    Item,
    Switch,
    Run,
    
}
