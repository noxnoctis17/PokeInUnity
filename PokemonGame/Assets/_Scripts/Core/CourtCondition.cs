using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class CourtCondition
{
    public CourtConditionID ID { get; set; }
    public int Duration { get; private set; }
    public int DurationModifier { get; private set; }
    public int TimeLeft { get; set; }
    public string StartMessage { get; set; }
    public string EndMessage { get; set; }
    public Action<BattleSystem, Battlefield, CourtLocation, BattleUnit> OnStart { get; set; }
    public Action<BattleSystem, Battlefield> OnEnd { get; set; }
    public Action<BattleUnit, Battlefield> OnEnterCourt { get; set; }
    public Action<BattleUnit, Battlefield> OnExitCourt { get; set; }

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
