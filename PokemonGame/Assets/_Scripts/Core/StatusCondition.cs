using System;
using UnityEngine;

public class StatusCondition
{
    public string Name { get; set; }
    public StatusConditionID ID { get; set; }
    public string Description { get; set; }
    public string StartMessage { get; set; }
    public string EffectMessage { get; set; }
    public string EndMessage { get; set; }
    public Sprite StatusIcon { get; set; }
    public GameObject VFX { get; set; }

    public Action<Pokemon> OnApplyStatus { get; set; }
    public Action<Pokemon> OnStart { get; set; }
    public Func<Pokemon, bool> OnBeforeTurn { get; set; }
    public Action<Pokemon> OnAfterTurn { get; set; }
}
