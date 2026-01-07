using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public enum ConditionType { AllySide_Buff, OpposingSide_Debuff, OpposingSide_Hazard }
public class CourtCondition
{
    public CourtConditionID ID { get; set; }
    public ConditionType ConType { get; set; }
    public int Duration { get; private set; }
    public int DurationModifier { get; private set; }
    public int TimeLeft { get; set; }
    public bool IsInfinite { get; set; } //--For hazards, as they do not have a timed duration on the field
    public string StartMessage { get; set; }
    public Func<BattleSystem, Pokemon, string> TrickRoomStartMessage { get; set; }
    public Func<BattleSystem, Pokemon, string> TrickRoomAlreadyActiveMessage { get; set; }
    public string EffectMessage { get; set; }
    public string EndMessage { get; set; }
    public Action<BattleSystem, Battlefield, CourtLocation, BattleUnit> OnStart { get; set; }
    public Action<BattleSystem, Battlefield> OnEnd { get; set; }
    public Action<BattleUnit, Battlefield> OnEnterCourt { get; set; }
    public Action<BattleUnit, Battlefield> OnExitCourt { get; set; }
    public Action<BattleUnit, Battlefield, CourtLocation> OnCourtEffect { get; set; }

    public CourtCondition( int duration, int modifier )
    {
        Duration = duration;
        DurationModifier = modifier;
    }

    public void SetTimeLeft( int duration )
    {
        TimeLeft = duration;
    }

}
