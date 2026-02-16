using System;
using UnityEngine;

public class VolatileCondition
{
    public string Name { get; set; }
    public VolatileConditionID ID { get; set; }
    public int Duration { get; set; }
    public string Description { get; set; }
    public string StartMessage { get; set; }
    public string EffectMessage { get; set; }
    public string EndMessage { get; set; }
    public bool Passable { get; set; }
    public GameObject VFX { get; set; }

    public Action<BattleUnit, BattleUnit, BattleSystem> OnApplyStatus { get; set; } //-- i think we can use this one to get battle units and all that as opposed to on start. we can block or change stuff here.
    public Action<Pokemon> OnStart { get; set; }
    public Action<Pokemon> OnEnter { get; set; }
    public Action<Pokemon> OnExit { get; set; }
    public Func<Pokemon, bool> OnBeforeTurn { get; set; }
    public Func<BattleUnit, Move> OnBeforeMoveUsed { get; set; }
    public Action<Pokemon> OnAfterTurn { get; set; }
    public Action<BattleUnit> OnRoundEndPhase { get; set; }
    public Func<BattleUnit, int, int> OnTakeDamage { get; set; }
    public Func<BattleUnit, BattleUnit, Move, BattleSystem, int> OnModifyMovePower { get; set; }
}
