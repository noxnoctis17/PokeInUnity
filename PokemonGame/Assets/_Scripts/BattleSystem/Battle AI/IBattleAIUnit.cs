using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IBattleAIUnit
{
    public string Name { get; set; }
    public float CurrentHPR { get; set; }
    public ( PokemonType One, PokemonType Two ) Type { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int SpAttack { get; set; }
    public int SpDefense { get; set; }
    public int Speed { get; set; }
    public MoveThreatResult MTR { get; set; }
    public List<Move> ActiveMoves { get; set; }
    public bool HasPriority { get; set; }
    public bool IsUngrounded { get; set; }

    public AbilityID Ability { get; set; }
    public BattleItemEffectID Item { get; set; }

    public SevereConditionID SevereStatus { get; set; }
    public int SevereStatusDuration { get; set; }
    public List<VolatileConditionID> VolatileStatuses { get; set; }
    public List<BindingConditionID> Bindings { get; set; }

    public CourtLocation CourtLocation { get; set; }
    public Court Court { get; set; }
    public bool CourtSeeded { get; set; }

    public StatStageDelta StatStages { get; set; }

    public void Build( SimulatedUnit unit )
    {
        
    }

    public void Build( Pokemon mon )
    {
        
    }
}
