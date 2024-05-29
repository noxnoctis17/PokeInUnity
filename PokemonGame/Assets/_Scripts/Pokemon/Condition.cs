using System;
using UnityEngine;

public class Condition
{
    public string Name { get; set; }
    public ConditionID ID { get; set; }
    public string Description { get; set; }
    public string StartMessage { get; set; }
    public string EffectMessage { get; set; }
    public string EndMessage { get; set; }
    public Sprite StatusIcon { get; set; }

    public Action<Pokemon> OnApplyStatus { get; set; }
    public Action<Pokemon> OnStart { get; set; }
    public Func<Pokemon, bool> OnBeforeTurn { get; set; }
    public Action<Pokemon> OnAfterTurn { get; set; }
    public Action<Pokemon> OnWeather { get; set; }
    public Action<Pokemon> OnEnterWeather { get; set; }
    public Action<Pokemon> OnExitWeather { get ; set;}
    public Func<Pokemon, Pokemon, Move, float> OnDamageModify { get; set; }
}
