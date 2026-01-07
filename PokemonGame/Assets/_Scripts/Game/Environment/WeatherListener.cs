using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using UnityEngine;

public class WeatherListener : MonoBehaviour
{
    [SerializeField] private Canvas _sunCanvas;
    [SerializeField] private GameObject _harshSunlight_VFX;
    [SerializeField] private GameObject _rainfall_VFX;
    [SerializeField] private GameObject _sandstorm_VFX;
    [SerializeField] private GameObject _snowfall_VFX;
    [SerializeField] private WeatherConditionID _currentWeather;
    [SerializeField] private WeatherConditionID _defaultAreaWeather;
    private GameObject _currentWeatherVFX;
    public WeatherConditionID CurrentWeather => _currentWeather;
    public WeatherConditionID DefaultAreaWeather => _defaultAreaWeather;

    private void Start()
    {
        WeatherController.Instance.OnChangeWeather += ChangeWeather;
        WeatherController.Instance.OnSubscribeToWC?.Invoke( this );
        _sunCanvas.worldCamera = PlayerReferences.MainCameraTransform.GetComponent<Camera>();
        
        ChangeWeather( _defaultAreaWeather );
    }

    private void OnDisable()
    {
        WeatherController.Instance.OnChangeWeather -= ChangeWeather;
        WeatherController.Instance.OnLeaveWC?.Invoke( this );
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
