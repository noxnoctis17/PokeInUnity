using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_PoisonDamage : IRoundEndPhaseHandler
{
    public void OnUnitTick( BattleSystem battleSystem, BattleUnit unit )
    {
        if( unit.Pokemon.SevereStatus?.ID == SevereConditionID.PSN || unit.Pokemon.SevereStatus?.ID == SevereConditionID.TOX )
        {
            int prevHp = unit.Pokemon.CurrentHP;
            unit.Pokemon.SevereStatus?.OnAfterTurn?.Invoke( unit.Pokemon );
            int damage = prevHp - unit.Pokemon.CurrentHP;
            
            if( damage != 0 )
                unit.Pokemon.BattleItemEffect?.OnTakePassiveDamage?.Invoke( unit, damage );
        }
    }
}
