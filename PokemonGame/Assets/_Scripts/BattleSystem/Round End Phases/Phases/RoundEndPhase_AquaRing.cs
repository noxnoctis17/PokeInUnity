using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_AquaRing : IRoundEndPhaseHandler
{
    public void OnUnitTick( BattleSystem battleSystem, BattleUnit unit )
    {
        if( unit.Pokemon.VolatileStatuses != null && unit.Pokemon.VolatileStatuses.Count > 0 )
        {
            foreach( var kvp in unit.Pokemon.VolatileStatuses )
            {
                var status = kvp.Value.Condition;
                status?.OnRoundEndPhase?.Invoke( unit );
            }
        }
    }
}
