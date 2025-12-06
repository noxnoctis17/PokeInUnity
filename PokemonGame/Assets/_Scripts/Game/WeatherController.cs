using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeatherController : MonoBehaviour
{
    public static WeatherController Instance;
    [SerializeField] private GameObject _harshSunlight_VFX;
    [SerializeField] private GameObject _rainfall_VFX;
    [SerializeField] private GameObject _sandstorm_VFX;
    [SerializeField] private GameObject _snowfall_VFX;
    [SerializeField] private WeatherConditionID _currentWeather;
    [SerializeField] private WeatherConditionID _defaultAreaWeather;
    private GameObject _currentWeatherVFX;
    public WeatherConditionID CurrentWeather => _currentWeather;
    public WeatherConditionID DefaultAreaWeather => _defaultAreaWeather;
    public Action<WeatherConditionID> OnChangeWeather;

    /// <summary>
    /// eventually what i should do is create "weather profiles" using a ScriptableObject, that i can use to define lighting temperature/color,
    /// the appropriate vfx, cloud density in the sky, etc.
    /// and by eventually i mean maybe very soon lol
    /// </summary>

    private void Start()
    {
        Instance = this;
        OnChangeWeather += ChangeWeather;
        ChangeWeather( _defaultAreaWeather );
    }

    private void FixedUpdate()
    {
        FollowPlayer();
    }

    private void FollowPlayer()
    {
        var playerPosX = PlayerReferences.Instance.PlayerCenter.position.x;
        var playerPosY = PlayerReferences.Instance.PlayerCenter.position.y + 10f;
        var playerPosZ = PlayerReferences.Instance.PlayerCenter.position.z;

        if( _currentWeatherVFX != null )
            _currentWeatherVFX.transform.position = new Vector3( playerPosX, playerPosY, playerPosZ );
    }

    private void ChangeWeather( WeatherConditionID weatherID )
    {
        if( _currentWeatherVFX != null )
        {
            Debug.Log( $"_currentWeatherVFX is: {_currentWeatherVFX}, and its weather is: {_currentWeatherVFX.name}" );
            _currentWeatherVFX.SetActive( false );
            _currentWeatherVFX = null;
        }

        _currentWeather = weatherID;

        switch( _currentWeather )
        {
            case WeatherConditionID.NONE:
                if( _defaultAreaWeather != WeatherConditionID.NONE )
                {
                    if( _currentWeatherVFX != null )
                    {
                        _currentWeatherVFX.SetActive( false );
                        _currentWeatherVFX = null;
                    }
                    
                    _currentWeather = _defaultAreaWeather;
                    ChangeWeather( _currentWeather );
                }
                else
                    _currentWeatherVFX = null;
            break;

            case WeatherConditionID.SUNNY:
                if( _harshSunlight_VFX != null )
                    _currentWeatherVFX = _harshSunlight_VFX;
            break;

            case WeatherConditionID.RAIN:
                if( _rainfall_VFX != null )
                    _currentWeatherVFX = _rainfall_VFX;
            break;

            case WeatherConditionID.SANDSTORM:
                if( _sandstorm_VFX != null )
                    _currentWeatherVFX = _sandstorm_VFX;
            break;

            case WeatherConditionID.SNOW:
                if( _snowfall_VFX != null )
                    _currentWeatherVFX = _snowfall_VFX;
            break;
        }

        if( _currentWeatherVFX != null )
        {
            _currentWeatherVFX.transform.rotation = Quaternion.identity;
            _currentWeatherVFX.SetActive( true );
        }
    }
}
