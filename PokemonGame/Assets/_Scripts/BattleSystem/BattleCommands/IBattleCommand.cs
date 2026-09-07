
//-----------------------------------------------------------------------------
//-------------------------Battle Command Interface----------------------------
//-----------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;

public interface IBattleCommand
{
    public BattleUnit User { get; }
    public int CommandPriority { get; }
    public int AttackPriority { get; }
    public int UnitSpeed { get; }
    public bool AfterYou { get; }
    public bool Quash { get; }

    public IEnumerator ExecuteBattleCommand();
    public void ChangeTarget( BattleUnit target ){}
}

public enum CommandPriorityEnum
{
    Attack,
    Item,
    Switch,
    Run,
}
