using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_StatusDuration : IRoundEndPhaseHandler
{
    public void OnUnitTick( BattleSystem battleSystem, BattleUnit unit )
    {
        //--Tick down Volatile Status durations
        foreach( var kvp in unit.Pokemon.VolatileStatuses )
        {
            var status = kvp.Value.Condition;
            status?.OnAfterTurn?.Invoke( unit.Pokemon );
        }

        //--Tick down Severe Status durations of Sleep and Paralysis
        if( unit.Pokemon.SevereStatus?.ID == SevereConditionID.SLP || unit.Pokemon.SevereStatus?.ID == SevereConditionID.PAR )
        {
            unit.Pokemon.SevereStatus?.OnAfterTurn?.Invoke( unit.Pokemon );
        }
    }
}
