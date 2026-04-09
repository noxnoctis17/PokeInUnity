using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_AfterNextRoundActivate : IRoundEndPhaseHandler
{
    public void OnUnitTick( BattleSystem battleSystem, BattleUnit unit )
    {
        Debug.Log( $"[Wish] AfterNextRoundActivate. Unit: {unit.Pokemon.NickName}'s After Nex Round Queue Count: {unit.AfterNextRoundQueue.Count}" );
        if( unit.AfterNextRoundQueue.Count <= 0 )
            return;

        int count = unit.AfterNextRoundQueue.Count;

        for( int i = 0; i < count; i++ )
        {
            var next = unit.AfterNextRoundQueue.Dequeue(); 
            
            Debug.Log( $"[Wish] next count: {next.Count}" );

            if( next.Count > 0 )
                unit.AfterNextRoundQueue.Enqueue( next );

            if( MoveConditionDB.Conditions.ContainsKey( next.Move.MoveSO.Name ) )
                MoveConditionDB.Conditions[next.Move.MoveSO.Name]?.OnAfterRound?.Invoke( unit, next.Move, battleSystem );

        }
    }
}
