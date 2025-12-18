
//-----------------------------------------------------------------------------
//-------------------------Battle Command Interface----------------------------
//-----------------------------------------------------------------------------
using System.Collections;

public interface IBattleCommand
{
    public BattleUnit User { get; }
    public int CommandPriority { get; }
    public int AttackPriority { get; }
    public int UnitAgility { get; }

    public IEnumerator ExecuteBattleCommand();
    public void ChangeTarget( BattleUnit target );

}

public enum CommandPriorityEnum{
    Attack,
    Item,
    Switch,
    Run,
    
}
