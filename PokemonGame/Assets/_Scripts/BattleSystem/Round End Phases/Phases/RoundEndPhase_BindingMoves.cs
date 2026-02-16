using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_BindingMoves : IRoundEndPhaseHandler
{
    public void OnUnitTick( BattleSystem battleSystem, BattleUnit unit )
    {
        if( unit.Pokemon.BindingStatuses != null && unit.Pokemon.BindingStatuses.Count > 0 )
        {
            foreach( var kvp in unit.Pokemon.BindingStatuses )
            {
                var status = kvp.Value.Condition;
                int prevHp = unit.Pokemon.CurrentHP;

                status?.OnAfterTurn?.Invoke( unit.Pokemon );
                int damage = prevHp - unit.Pokemon.CurrentHP;

                if( damage != 0 )
                    unit.Pokemon.BattleItemEffect?.OnTakePassiveDamage?.Invoke( unit, damage );
            }
        }
    }
}
