using System;
using UnityEngine.UI;

public class ConditionClass
{
    public string ConditionName { get; set; }
    public ConditionID ID { get; set; }
    public string Description { get; set; }
    public string StartMessage { get; set; }
    public Image SevereStatusIcon { get; set; }
    public Action<PokemonClass> OnRoundStart { get; set; }
    public Func<PokemonClass, bool> OnBeforeTurn { get; set; }
    public Action<PokemonClass> OnAfterTurn { get; set; }
}
