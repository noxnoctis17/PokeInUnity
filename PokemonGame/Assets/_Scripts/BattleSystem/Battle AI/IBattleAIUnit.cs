using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IBattleAIUnit
{
    public Pokemon Pokemon { get; set; }
    public string Name { get; set; }
    public string PID { get; set; }
    public float BeginningHPR { get; set; }
    public float EndHPR { get; set; }
    public ( PokemonType One, PokemonType Two ) Type { get; set; }
    public int Level { get; set; }
    public int HP { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int SpAttack { get; set; }
    public int SpDefense { get; set; }
    public int Speed { get; set; }
    public RoleProfile RoleProfile { get; set; }
    public StatSpread StatSpread { get; set; }
    public MoveThreatResult MTR { get; set; }
    public List<Move> ActiveMoves { get; set; }
    public bool HasPriority { get; set; }
    public bool IsUngrounded { get; set; }

    public float Expendability { get; set; }

    public AbilityID Ability { get; set; }
    public ItemBattleEffectID Item { get; set; }

    public SevereConditionID SevereStatus { get; set; }
    public int SevereStatusTime { get; set; }
    public List<VolatileConditionID> VolatileStatuses { get; set; }
    public List<BindingConditionID> Bindings { get; set; }

    public CourtLocation CourtLocation { get; set; }

    public Dictionary<Stat, int> StatStages { get; set; }
    public Dictionary<Stat, Dictionary<DirectModifierCause, float>> DirectStatModifiers{ get; set; }
}
