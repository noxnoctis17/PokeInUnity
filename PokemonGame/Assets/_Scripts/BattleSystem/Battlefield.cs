
using System;
using System.Collections.Generic;

public class Battlefield
{
    public WeatherCondition Weather { get; set; }
    public int? WeatherDuration { get; set; }
    public Dictionary<CourtLocation, Court> ActiveCourts { get; set; }

    public void SetWeather( WeatherConditionID id, int duration = 5 ){
        Weather = WeatherConditionsDB.Conditions[id];
        Weather.ID = id;
        WeatherDuration = duration;

        if( WeatherController.Instance.CurrentWeather != id )
            WeatherController.Instance.OnChangeWeather?.Invoke( id );
    }

    public void AddCourts( CourtLocation location, List<BattleUnit> units )
    {
        ActiveCourts.Add( location, new( location, units ) );
    }

    public Court GetUnitCourt( BattleUnit unit )
    {
        foreach( var court in ActiveCourts.Values )
            if( court.Units.Contains( unit ) )
                return court;

        return null;
    }
}
