using System;
using UnityEngine;

public class WeatherController : MonoBehaviour
{
    public static WeatherController Instance;
    public WeatherListener CurrentListener { get; private set; }
    public WeatherConditionID CurrentWeather { get; private set; }
    public Action<WeatherConditionID> OnChangeWeather;
    public Action<WeatherListener> OnSubscribeToWC;
    public Action<WeatherListener> OnLeaveWC;

    //--eventually what i should do is create "weather profiles" using a ScriptableObject, that i can use to define lighting temperature/color,
    //--the appropriate vfx, cloud density in the sky, etc.
    //--and by eventually i mean maybe very soon lol --still not done as of 12/22/25, said this shit in like november
    //--also need to make a WeatherListener class that is active in each scene. the Weather Controller class will simply put out a static event with the weather id
    //--the weather listener will handle turning the weather effects on or off.

    private void OnEnable()
    {
        Instance = this;
        OnChangeWeather += SetCurrentWeather;
        OnSubscribeToWC += AssignCurrentListener;
        OnLeaveWC += LeaveCurrentListener;
    }

    private void SetCurrentWeather( WeatherConditionID currentWeather )
    {
        CurrentWeather = currentWeather;
    }

    private void AssignCurrentListener( WeatherListener listener )
    {
        CurrentListener = listener;
    }

    private void LeaveCurrentListener( WeatherListener listener )
    {
        if( CurrentListener == listener )
            CurrentListener = null;
    }
}
