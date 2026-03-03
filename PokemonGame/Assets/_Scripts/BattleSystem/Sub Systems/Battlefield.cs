
using System.Collections.Generic;
using UnityEngine;

public class Battlefield
{
    public WeatherCondition Weather { get; set; }
    public int? WeatherDuration { get; set; }
    public TerrainCondition Terrain { get; set; }
    public int? TerrainDuration { get; set; }
    public Dictionary<CourtLocation, Court> ActiveCourts { get; private set; }
    
    public Battlefield( BattleSystem bs )
    {
        ActiveCourts = new();
    }

    public void SetWeather( WeatherConditionID id, int duration = 5 )
    {
        ExitWeather();

        Weather = WeatherConditionsDB.Conditions[id];
        Weather.ID = id;
        WeatherDuration = duration;

        EnterWeather();

        if( WeatherController.Instance.CurrentWeather != id )
            WeatherController.Instance.OnChangeWeather?.Invoke( id );
    }

    private void EnterWeather()
    {
        WeatherConditionID currentWeather = WeatherConditionID.None;

        if( Weather != null && currentWeather != Weather?.ID )
        {
            if( Weather?.StartByMoveMessage != null )
                BattleSystem.Instance.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( Weather?.StartByMoveMessage ) );

            currentWeather = Weather.ID;
        }

        foreach( var court in ActiveCourts )
        {
            foreach( var unit in court.Value.Units )
            {
                Weather?.OnEnterWeather?.Invoke( unit.Pokemon );
            }
        }
    }

    private void ExitWeather()
    {
        foreach( var court in ActiveCourts )
        {
            foreach( var unit in court.Value.Units )
            {
                Weather?.OnExitWeather?.Invoke( unit.Pokemon );
            }
        }
    }

    public void SetTerrain( TerrainID id, int duration = 5 )
    {
        ExitTerrain();

        Terrain = TerrainDB.Terrains[id];
        Terrain.ID = id;
        TerrainDuration = duration;

        TerrainManager.Instance.DisplayTerrain( id );

        EnterTerrain();
    }

    private void EnterTerrain()
    {
        TerrainID currentTerrain = TerrainID.None;
        if( Terrain != null && currentTerrain != Terrain?.ID )
        {
            if( Terrain?.StartByMoveMessage != null )
                BattleSystem.Instance.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( Terrain?.StartByMoveMessage ) );

            currentTerrain = Terrain.ID;
        }

        foreach( var court in ActiveCourts )
        {
            foreach( var unit in court.Value.Units )
            {
                Terrain?.OnEnterTerrain?.Invoke( unit );
            }
        }
    }

    private void ExitTerrain()
    {
        foreach( var court in ActiveCourts )
        {
            foreach( var unit in court.Value.Units )
            {
                Terrain?.OnExitTerrain?.Invoke( unit );
            }
        }
    }

    public void AddCourts( CourtLocation location, List<BattleUnit> units, BattleTrainer trainer )
    {
        ActiveCourts.Add( location, new( location, units, trainer ) );
    }

    public Court GetUnitCourt( BattleUnit unit )
    {
        foreach( var court in ActiveCourts.Values )
            if( court.Units.Contains( unit ) )
                return court;

        return null;
    }

    public Court GetUnitCourt( Pokemon pokemon )
    {
        foreach( var court in ActiveCourts.Values )
        {
            for( int i = 0; i < court.Units.Count; i++ )
            {
                if( court.Units[i].Pokemon == pokemon )
                    return court;
            }
        }
            

        return null;
    }

    public CourtLocation GetOpposingCourtLocation( BattleUnit unit )
    {
        foreach( var court in ActiveCourts.Values )
        {
            if( court.Units.Contains( unit ) )
            {
                if( court.Location == CourtLocation.TopCourt )
                    return CourtLocation.BottomCourt;
                else
                    return CourtLocation.TopCourt;
            }
        }

        return default;
    }

    public List<BattleUnit> GetOpposingUnits( BattleUnit unit )
    {
        foreach( var court in ActiveCourts.Values )
        {
            if( court.Units.Contains( unit ) )
            {
                if( court.Location == CourtLocation.TopCourt )
                    return ActiveCourts[CourtLocation.BottomCourt].Units;
                else
                    return ActiveCourts[CourtLocation.TopCourt].Units;
            }
        }

        return null;
    }

    public Court GetPokemonCourtFromTrainer( Pokemon pokemon )
    {
        foreach( var court in ActiveCourts )
        {
            if( court.Value.Trainer != null && court.Value.Trainer.Party.Contains( pokemon ) )
                return court.Value;
            else
                continue;
        }

        return null;
    }

    public CourtLocation GetPokemonCourtLocationFromTrainer( Pokemon pokemon )
    {
        foreach( var court in ActiveCourts )
        {
            if( court.Value.Trainer != null && court.Value.Trainer.Party.Contains( pokemon ) )
                return court.Value.Location;
            else
                continue;
        }

        return CourtLocation.TopCourt; //--Default to top court. if no trainer is found or the trainer for some reason doesn't have the mon, it's likely an npc/wild battle or something
    }

}
