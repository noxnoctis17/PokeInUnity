using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_CourtDuration : IRoundEndPhaseHandler
{
    public void OnPhaseTick( BattleSystem battleSystem )
    {
        //--Theoretically there should always be courts active, they just might not have any conditions in their dictionaries
        if( battleSystem.Field.ActiveCourts.Count > 0 )
        {
            List<( CourtLocation location, CourtConditionID conditionID )> courtConditionsToRemove = new();
            foreach( var activeCourt in battleSystem.Field.ActiveCourts )
            {
                var court = activeCourt.Value;

                if( court.Conditions.Count > 0 )
                {
                    foreach( var courtCondition in court.Conditions )
                    {
                        var condition = courtCondition.Value;

                        if( condition.IsInfinite )
                        {
                            continue;
                        }
                        else if( condition.TimeLeft > 0 )
                        {
                            Debug.Log( $"Reducing {court}'s {condition.ID}'s Time Left from {condition.TimeLeft} to {( condition.TimeLeft - 1 )}" );
                            condition.TimeLeft--;
                        }
                        else if( condition.TimeLeft == 0 )
                        {
                            if( condition.EndMessage != null )
                            {
                                condition?.OnEnd?.Invoke( battleSystem, battleSystem.Field );
                                string message = condition.EndMessage;
                                battleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( message ) );
                            }

                            foreach( var unit in court.Units )
                            {
                                condition?.OnExitCourt?.Invoke( unit, battleSystem.Field );
                            }

                            courtConditionsToRemove.Add( ( activeCourt.Key, courtCondition.Key ) );
                        }
                    }
                }
            }

            if( courtConditionsToRemove.Count > 0 )
            {
                foreach( var (location, conditionID) in courtConditionsToRemove )
                {
                    battleSystem.Field.ActiveCourts[location].RemoveCondition( conditionID );
                }
            }
        }
    }
}
