using System;
using UnityEngine;
using UnityEngine.UI;

public class Condition
{
    public string ConditionName { get; set; }
    public ConditionID ID { get; set; }
    public string Description { get; set; }
    public string AfflictionDialogue { get; set; }
    public Sprite StatusIcon { get; set; }
    public Action<Pokemon> OnRoundStart { get; set; }
    public Func<Pokemon, bool> OnBeforeTurn { get; set; }
    public Action<Pokemon> OnAfterTurn { get; set; }
}
