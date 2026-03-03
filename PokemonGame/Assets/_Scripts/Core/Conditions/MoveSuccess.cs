using System;
using UnityEngine;

public class MoveSuccess
{
    public string Name { get; set; }
    public Func<Pokemon, string> SuccessMessage { get; set; }
    public Func<Pokemon, string> FailureMessage { get; set; }

    //--Attacker, Target, Move Used, BattleSystem ref, Battlefield ref, return success
    public Func<BattleUnit, BattleUnit, Move, BattleSystem, bool> OnCheckSuccess { get; set; } = ( _, _, _, _ ) => true;
    public Action<BattleUnit, BattleUnit, Move, BattleSystem> OnCheckAccuracy { get; set; }
    public Action<BattleUnit, BattleUnit, Move, BattleSystem> OnMoveCompleted { get; set; }
    public Func<BattleUnit, BattleUnit, Move, BattleSystem, bool> OnCheckNeedsToCharge { get; set; } = ( _, _, _, _ ) => true;
    public Func<BattleUnit, BattleUnit, Move, BattleSystem, bool> OnCheckChargeSuccessSkip { get; set; } = ( _, _, _, _ ) => false;
}
