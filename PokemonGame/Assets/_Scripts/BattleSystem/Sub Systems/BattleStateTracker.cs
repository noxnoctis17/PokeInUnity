using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class BattleStateTracker : MonoBehaviour
{
    [SerializeField] private BattleSystem _battleSystem;
    private Battlefield _field;
    [SerializeField] private Image _currentWeatherIcon;
    [SerializeField] private Sprite _clearWeatherIcon;
    [SerializeField] private Sprite _harshSunIcon;
    [SerializeField] private Sprite _rainfallIcon;
    [SerializeField] private Sprite _sandstormIcon;
    [SerializeField] private Sprite _snowfallIcon;
    [SerializeField] private TextMeshProUGUI _weatherDurationText;
    private bool _trackWeather;

    private void OnDisable()
    {
        WeatherController.Instance.OnChangeWeather -= SetWeatherTracker;
    }

    private void Start()
    {
        WeatherController.Instance.OnChangeWeather += SetWeatherTracker;
        _field = _battleSystem.Field;

        if( _field.Weather != null )
            SetWeatherTracker( _field.Weather.ID );
    }

    private void Update()
    {
        if( _trackWeather )
            TrackWeatherDuration();
    }

    private void TrackWeatherDuration()
    {
        _weatherDurationText.text = $"{_field.WeatherDuration}/5";
    }

    private void SetWeatherTracker( WeatherConditionID id )
    {
        switch( id )
        {
            case WeatherConditionID.None:
                _currentWeatherIcon.sprite = _clearWeatherIcon;
                _weatherDurationText.text = "";
                _trackWeather = false;
            break;

            case WeatherConditionID.SUNNY:
                _currentWeatherIcon.sprite = _harshSunIcon;
                // _weatherDurationText.text = $"{_field.WeatherDuration}/5";
                _trackWeather = true;
            break;

            case WeatherConditionID.RAIN:
                _currentWeatherIcon.sprite = _rainfallIcon;
                // _weatherDurationText.text = $"{_field.WeatherDuration}/5";
                _trackWeather = true;
            break;

            case WeatherConditionID.SANDSTORM:
                _currentWeatherIcon.sprite = _sandstormIcon;
                // _weatherDurationText.text = $"{_field.WeatherDuration}/5";
                _trackWeather = true;
            break;

            case WeatherConditionID.SNOW:
                _currentWeatherIcon.sprite = _snowfallIcon;
                // _weatherDurationText.text = $"{_field.WeatherDuration}/5";
                _trackWeather = true;
            break;
        }
    }
}
