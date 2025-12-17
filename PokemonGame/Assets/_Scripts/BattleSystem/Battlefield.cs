
using System.Collections.Generic;

public class Battlefield
{
    public WeatherCondition Weather { get; set; }
    public int? WeatherDuration { get; set; }
    public Dictionary<CourtLocation, Court> ActiveCourts { get; set; }

    public void SetWeather( WeatherConditionID id, int duration = 5 ){
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
}
