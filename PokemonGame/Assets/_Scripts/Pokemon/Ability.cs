using System;
using System.Collections.Generic;

public class Ability
{
    public string Name { get; set; }
    public AbilityID ID { get; set; }
    public string Description { get; set; }
    public Action<Pokemon> OnAbilityTriggered { get; set; }
//--Temporary Stat Modifications---------------------------------------------------
    public Func<float, Pokemon, Pokemon, Move, float> OnModify_ATK { get; set; }
    public Func<float, Pokemon, Pokemon, Move, float> OnModify_SpATK { get; set; }
    public Func<float, Pokemon, Pokemon, Move, float> OnModify_DEF { get; set; }
    public Func<float, Pokemon, Pokemon, Move, float> OnModify_SpDEF { get; set; }
    public Func<float, Pokemon, Pokemon, Move, float> OnModify_SPD { get; set; }
    public Func<float, Pokemon, Pokemon, Move, float> OnModify_ACC { get; set; }
    public Func<float, Pokemon, Pokemon, Move, float> OnModify_EVA { get; set; }
    public Func<float, Pokemon, Pokemon, Move, float> OnIncomingDamage { get; set; }
//--Stat Stage Modification---------------------------------------------------------
    public Action<Dictionary<Stat, int>, Pokemon, Pokemon> OnStatStageChange { get; set; }
//--Triggers on Entering the field--------------------------------------------------
    public Action<Pokemon, List<BattleUnit>, Battlefield> OnAbilityEnter { get; set; }
    public Action<Pokemon, List<BattleUnit>, Battlefield> OnAbilityExit { get; set; }
    public Action<BattleUnit, BattleUnit, Battlefield> OnAbilityBeforeTurn { get; set; }
    public Action<BattleUnit, Battlefield> OnAbilityAfterTurn { get; set; }
    public Func<StatusConditionID, Pokemon, EffectSource, bool> OnTrySetSevereStatus { get; set; }
    public Func<StatusConditionID, Pokemon, EffectSource, bool> OnTrySetVolatileStatus { get; set; }
    public Func<StatusConditionID, Pokemon, EffectSource, bool> OnTrySetTransientStatus { get; set; }
    public Action<BattleUnit, BattleUnit, Move> OnMoveContact { get; set; }
    public Func<Pokemon, Move, float> OnSTABModify { get; set; }
    public Func<float> OnSecondaryEffectChanceModify { get; set; }
}
