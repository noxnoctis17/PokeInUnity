using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_StatusOrbs : IRoundEndPhaseHandler
{
    public void OnUnitTick( BattleSystem battleSystem, BattleUnit unit )
    {
        if( unit.Pokemon.HeldItem != null && ( unit.Pokemon.HeldItem.ItemName == "Flame Orb" || unit.Pokemon.HeldItem.ItemName == "Toxic Orb" || unit.Pokemon.HeldItem.ItemName == "Static Orb" ) )
            unit.Pokemon.BattleItemEffect?.OnItemRoundEnd?.Invoke( unit.Pokemon );
    }
}
