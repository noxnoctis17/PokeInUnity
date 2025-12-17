using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_StatusDamage : IRoundEndPhaseHandler
{
    public void Apply( BattleSystem battleSystem, BattleUnit unit )
    {
        unit.Pokemon.SevereStatus?.OnAfterTurn?.Invoke( unit.Pokemon );
        unit.Pokemon.VolatileStatus?.OnAfterTurn?.Invoke( unit.Pokemon );
    }
}
