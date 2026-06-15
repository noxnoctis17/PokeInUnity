using System;

public class FieldCondition
{
    public string Name { get; set; }
    public FieldConditionID ID { get; set; }
    public int Duration { get; private set; }
    public int DurationModifier { get; private set; }
    public int TimeLeft { get; set; }
    public bool IsInfinite { get; set; }
    public string Description { get; set; }
    public Func<BattleSystem, Pokemon, string> StartMessage { get; set; }
    public string EffectMessage { get; set; }
    public string SpecialMessage { get; set; }
    public string EndMessage { get; set; }
    public string StartByMoveMessage { get; set; }

    public Action<BattleSystem, Battlefield, BattleUnit> OnStart { get; set; }
    public Action<BattleSystem, Battlefield, BattleUnit> OnEnd { get; set; }
    public Action<Pokemon> OnFieldEffect { get; set; }
    public Action<Pokemon> OnEnterField { get; set; }
    public Action<Pokemon> OnExitField { get; set; }
    public Func<Pokemon, Pokemon, Move, float> OnDamageModify { get; set; }

    public FieldCondition( int duration, int modifier )
    {
        Duration = duration;
        DurationModifier = modifier;
    }
}
