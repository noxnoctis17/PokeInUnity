using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_TerrainDuration : IRoundEndPhaseHandler
{
    public void OnPhaseTick( BattleSystem battleSystem )
    {
        if( battleSystem.Field.Terrain != null )
        {
            if( battleSystem.Field.TerrainDuration > 0 )
            {
                if( battleSystem.Field.Terrain?.EffectMessage != null )
                {
                    string message = battleSystem.Field.Terrain?.EffectMessage;
                    battleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( message ) );
                }

                Debug.Log( $"[Round End Phase] Reducing {battleSystem.Field.Terrain?.ID}'s Time Left from {battleSystem.Field.TerrainDuration} to {battleSystem.Field.TerrainDuration - 1}" );
                battleSystem.Field.TerrainDuration--;
            }
            else if( battleSystem.Field.TerrainDuration == 0 )
            {
                if( battleSystem.Field.Terrain?.EndMessage != null )
                {
                    string message = battleSystem.Field.Terrain?.EndMessage;
                    battleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( message ) );
                }

                battleSystem.Field.SetTerrain( TerrainID.None );
                battleSystem.Field.Terrain = null;
                battleSystem.Field.TerrainDuration = null;
            }

        }
    }
}
