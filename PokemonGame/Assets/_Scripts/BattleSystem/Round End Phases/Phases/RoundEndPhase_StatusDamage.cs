using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_StatusDamage : IRoundEndPhaseHandler
{
    public void Apply( BattleSystem battleSystem, Pokemon pokemon )
    {
        pokemon.SevereStatus?.OnAfterTurn?.Invoke( pokemon );
        pokemon.VolatileStatus?.OnAfterTurn?.Invoke( pokemon );
    }
}
