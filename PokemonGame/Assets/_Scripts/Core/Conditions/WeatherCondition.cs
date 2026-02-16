using System;

public class WeatherCondition
{
    public string Name { get; set; }
    public WeatherConditionID ID { get; set; }
    public string Description { get; set; }
    public string StartMessage { get; set; }
    public string EffectMessage { get; set; }
    public string EndMessage { get; set; }
    public string StartByMoveMessage { get; set; }

    public Action<Pokemon> OnStart { get; set; }
    public Action<Pokemon> OnWeatherEffect { get; set; }
    public Action<Pokemon> OnEnterWeather { get; set; }
    public Action<Pokemon> OnExitWeather { get ; set;}
    public Func<Pokemon, Pokemon, Move, float> OnDamageModify { get; set; }

}
