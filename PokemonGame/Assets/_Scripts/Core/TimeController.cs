using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;

public class TimeController : MonoBehaviour
{
    [SerializeField] private Light _sun;
    [SerializeField] private Light _moon;
    [SerializeField] private float _maxSunLightIntensity;
    [SerializeField] private float _maxMoonLightIntensity;
    [SerializeField] private float _timeMultiplier;
    [SerializeField] private float _startHour;
    [SerializeField] private float _sunRiseHour;
    [SerializeField] private float _sunSetHour;
    [SerializeField] private TextMeshProUGUI _timeText;
    [SerializeField] private Color _dayAmbientLight;
    [SerializeField] private Color _nightAmbientLight;
    [SerializeField] private AnimationCurve _lightChangeCurve;
    private DateTime _currentTime;
    private TimeSpan _sunRiseTime;
    private TimeSpan _sunSetTime;

    private void Start(){
        _currentTime = DateTime.Now.Date + TimeSpan.FromHours( _startHour );
        _sunRiseTime = TimeSpan.FromHours( _sunRiseHour );
        _sunSetTime = TimeSpan.FromHours( _sunSetHour );

        _sun = LightReferences.Instance.SunTransform.GetComponent<Light>();
        _moon = LightReferences.Instance.MoonTransform.GetComponent<Light>();
        _timeText = UI_OverworldManager.Instance.TimeText;
    }

    private void Update(){
        UpdateTimeOfDay();
        RotateSun();
        UpdateLightSettings();
    }

    private void UpdateTimeOfDay(){
        _currentTime = _currentTime.AddSeconds( Time.deltaTime * _timeMultiplier );

        if( _timeText != null ){
            _timeText.text = _currentTime.ToString( "hh:mm" );
        }
    }

    private TimeSpan CalculateTimeDifference( TimeSpan fromTime, TimeSpan toTime ){
        TimeSpan difference = (toTime - fromTime);

        if( difference.TotalSeconds < 0  )
            difference += TimeSpan.FromHours( 24 );
        
        return difference;
    }

    private void RotateSun(){
        float sunRotation;

        if( _currentTime.TimeOfDay > _sunRiseTime && _currentTime.TimeOfDay < _sunSetTime ){
            TimeSpan sunRiseToSunSetDuration = CalculateTimeDifference( _sunRiseTime, _sunSetTime );
            TimeSpan timeSinceSunRise = CalculateTimeDifference( _sunRiseTime, _currentTime.TimeOfDay );

            double percentage = ( timeSinceSunRise.TotalMinutes / sunRiseToSunSetDuration.TotalMinutes );
            sunRotation = Mathf.Lerp( 0, 180, (float)percentage );

        } else {
            TimeSpan sunSetToSunRiseDuration = CalculateTimeDifference( _sunSetTime, _sunRiseTime );
            TimeSpan timeSinceSunSet = CalculateTimeDifference( _sunSetTime, _currentTime.TimeOfDay );

            double percentage = ( timeSinceSunSet.TotalMinutes / sunSetToSunRiseDuration.TotalMinutes );
            sunRotation = Mathf.Lerp( 180, 360, (float)percentage );
        }

        _sun.transform.rotation = Quaternion.AngleAxis( sunRotation, Vector3.right );
    }
    
    private void UpdateLightSettings(){
        float dotProduct = Vector3.Dot( _sun.transform.forward, Vector3.down );
        _sun.intensity = Mathf.Lerp( 0, _maxSunLightIntensity, _lightChangeCurve.Evaluate( dotProduct ) );
        _moon.intensity = Mathf.Lerp( _maxMoonLightIntensity, 0, _lightChangeCurve.Evaluate( dotProduct ) );
        RenderSettings.ambientLight = Color.Lerp( _nightAmbientLight, _dayAmbientLight, _lightChangeCurve.Evaluate( dotProduct ) );
    }
}
