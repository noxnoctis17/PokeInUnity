using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveCondition
{
    public string Name { get; set; }

    public Action<BattleUnit /*attacker*/, BattleUnit /*target*/, Move, BattleSystem> OnMoveSuccess { get; set; }
    public Action<BattleUnit /*attacker*/, BattleUnit /*target*/, Move, int /*damage*/, int /*hit*/, BattleSystem> OnMoveHitTarget { get; set; }
    public Func<BattleUnit /*attacker*/, BattleUnit /*target*/, Move, int /*hit*/, int> OnModifyMovePower { get; set; }
    public Func<BattleUnit /*attacker*/, BattleUnit /*target*/, Move, int, int> OnModifyMoveDamage { get; set; }
    public Func<BattleUnit, Move, BattleSystem, Move> OnMoveChanged { get; set; }
    public Action<BattleUnit /*attacker*/, BattleUnit /*target*/, Move, BattleSystem> OnMoveSuccessChanged { get; set; }
    public Action<BattleUnit /*attacker*/, BattleUnit /*target*/, Move, BattleSystem> OnMoveCompleted { get; set; }
    public Action<BattleUnit /*attacker*/, BattleUnit /*target*/, Move, BattleSystem> OnMoveEffectsChanged { get; set; }
    public Action<Dictionary<Stat, int>, Pokemon, Pokemon> OnStatStageChange { get; set; }
    public Func<BattleUnit, BattleUnit, Move, int> OnOverrideAttackingStat { get; set; }
    public Func<BattleUnit, BattleUnit, Move, int> OnOverrideDefensiveStat { get; set; }
    public Func<BattleUnit, BattleUnit, Move, BattleSystem, BattleUnit> OnTargetRedirect { get; set; }
    public Action<BattleUnit, BattleUnit, Move, BattleSystem> OnModifyCommandQueue { get; set; }
    public Action<BattleUnit, Move, BattleSystem> OnAfterNextRound { get; set; }

}