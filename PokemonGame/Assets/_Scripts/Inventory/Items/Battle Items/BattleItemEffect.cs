using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class BattleItemEffect
{
    public BattleItemEffectID ID { get; set; }
    public int Duration { get; private set; }
    public int DurationModifier { get; private set; }
    public int TimeLeft { get; set; }
    public bool IsInfinite { get; set; }
    public string StartMessage { get; set; }
    public string EffectMessage { get; set; }
    public string EndMessage { get; set; }
    public Action<BattleUnit> OnStart { get; set; }
    public Action<BattleUnit> OnEnd { get; set; }
    public Action<BattleUnit> OnItemEnter { get; set; }
    public Func<BattleUnit, BattleUnit, Move, bool> OnItemBeforeTurn { get; set; }
    public Action<Pokemon> OnItemTurnTurn { get; set; }
    public Action<BattleUnit> OnItemAfterTurn { get; set; }
    public Action<BattleUnit> OnItemExit { get; set; }
    public Action<Pokemon> OnItemRoundEnd { get; set; }
    public Func<BattleUnit, Pokemon, Move, float> OnDamageModify { get; set; }
    public Func<BattleUnit, BattleUnit, Move, float, int> OnTakeMoveDamage { get; set; }
    public Action<BattleUnit> OnAfterTakeDamage { get; set; }
    public Action<BattleUnit, float> OnTakePassiveDamage { get; set; }
    public Action<BattleUnit, BattleUnit, Move> OnMoveContact { get; set; }
}
