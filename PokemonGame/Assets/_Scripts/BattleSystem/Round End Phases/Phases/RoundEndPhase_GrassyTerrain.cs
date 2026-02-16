using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_GrassyTerrain : IRoundEndPhaseHandler
{
    public void OnUnitTick( BattleSystem battleSystem, BattleUnit unit )
    {
        Debug.Log( $"[Round End Phase] Checking for Grassy Terrain & Blighted Terrain" );
        if( battleSystem.Field.Terrain?.ID == TerrainID.Grassy && unit.Pokemon.CurrentHP < unit.Pokemon.MaxHP && !unit.Pokemon.IsFainted() )
            battleSystem.Field.Terrain?.OnTerrainEffect?.Invoke( unit );

        if( battleSystem.Field.Terrain?.ID == TerrainID.Blighted && unit.Pokemon.CurrentHP < unit.Pokemon.MaxHP && !unit.Pokemon.IsFainted() )
            battleSystem.Field.Terrain?.OnTerrainEffect?.Invoke( unit );   
    }
}
