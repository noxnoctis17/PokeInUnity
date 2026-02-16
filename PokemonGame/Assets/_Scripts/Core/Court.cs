using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public enum CourtLocation { TopCourt, BottomCourt }

public class Court
{
    public CourtLocation Location { get; private set; }
    public List<BattleUnit> Units { get; private set; }
    public Dictionary<CourtConditionID, CourtCondition> Conditions { get; private set; }

    public Court ( CourtLocation location, List<BattleUnit> units )
    {
        Location = location;
        Units = units;
        Conditions = new();
    }

    public void AddCondition( CourtConditionID condition )
    {
        Debug.Log( $"Adding {condition} to the {Location} Conditions!" );
        if( !Conditions.ContainsKey( condition ) )
            Conditions.Add( condition, CourtConditionDB.Conditions[condition] );
    }

    public void RemoveCondition( CourtConditionID condition )
    {
        Debug.Log( $"Removing {condition} from the {Location} Conditions!" );
        if( Conditions.ContainsKey( condition ) )
            Conditions.Remove( condition );
    }

    public int GetUnitIndex( BattleUnit unit )
    {
        return Units.FindIndex( u => u == unit );
    }

    //--The purpose of this class is to eventually house tailwind, screens, aurora veil, entry hazards, etc.
    //--Each "side" (player vs cpu, cpu vs cpu, player & cpu vs cpu & cpu, etc. ) will get their own court
    //--For now, i don't exactly need to track this just yet, but it will definitely be VERY soon lol --12/04/25
}
