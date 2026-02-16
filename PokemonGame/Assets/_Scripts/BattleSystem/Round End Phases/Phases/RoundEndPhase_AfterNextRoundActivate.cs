using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_AfterNextRoundActivate : IRoundEndPhaseHandler
{
    public void OnUnitTick( BattleSystem battleSystem, BattleUnit unit )
    {
        if( unit.AfterNextRoundQueue.Count == 0 )
            return;

        while( unit.AfterNextRoundQueue.Count > 0 )
        {
            var next = unit.AfterNextRoundQueue.Peek();
            
            if( next.Count > 0 )
                continue;

            if( MoveConditionDB.Conditions.ContainsKey( next.Move.MoveSO.Name ) )
            {
                MoveConditionDB.Conditions[next.Move.MoveSO.Name]?.OnAfterNextRound?.Invoke( unit, next.Move, battleSystem );
            }
        }
    }
}
