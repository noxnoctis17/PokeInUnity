using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_ItemRoundEnd : IRoundEndPhaseHandler
{
    public void Apply( BattleSystem battleSystem, BattleUnit unit )
    {
        unit.Pokemon.BattleItemEffect?.OnItemRoundEnd?.Invoke( unit.Pokemon );
    }
}
