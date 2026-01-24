using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_Leftovers : IRoundEndPhaseHandler
{
    public void OnUnitTick( BattleSystem battleSystem, BattleUnit unit )
    {
        if( unit.Pokemon.HeldItem != null && unit.Pokemon.HeldItem.ItemName == "Leftovers" )
            unit.Pokemon.BattleItemEffect?.OnItemRoundEnd?.Invoke( unit.Pokemon );
    }
}
