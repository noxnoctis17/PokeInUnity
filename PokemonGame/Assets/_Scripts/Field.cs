
public class Field
{
    public Condition Weather { get; set; }
    public int? WeatherDuration { get; set; }

    public void SetWeather( ConditionID id, int duration = 5 ){
        Weather = ConditionsDB.Conditions[id];
        Weather.ID = id;
        WeatherDuration = duration;
        Weather.OnStart?.Invoke( null );
    }
}
