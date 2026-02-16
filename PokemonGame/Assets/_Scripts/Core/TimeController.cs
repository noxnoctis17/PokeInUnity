using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;

public enum TimeOfDay { None, Dawn, Day, Dusk, Night }
public class TimeController : MonoBehaviour
{
    public static TimeController Instance;
    [SerializeField] private Light _sun;
    [SerializeField] private Light _moon;
    [SerializeField] private float _timeMultiplier;
    [SerializeField] private float _startHour;
    [SerializeField] private float _sunRiseHour;
    [SerializeField] private float _sunSetHour;
    [SerializeField] private TextMeshProUGUI _timeText;
    public DateTime CurrentTime { get; private set; }
    private TimeSpan _sunRiseTime;
    private TimeSpan _sunSetTime;
    private bool _battleActive;

    

    private void Start()
    {
        Instance = this;
        CurrentTime = DateTime.Now.Date + TimeSpan.FromHours( _startHour );
        _sunRiseTime = TimeSpan.FromHours( _sunRiseHour );
        _sunSetTime = TimeSpan.FromHours( _sunSetHour );

        _sun = LightReferences.Instance.SunTransform.GetComponent<Light>();
        _moon = LightReferences.Instance.MoonTransform.GetComponent<Light>();

        BattleSystem.OnBattleStarted += BattleStarted;
        BattleSystem.OnBattleEnded += BattleEnded;
    }

    private void Update(){
        if( _battleActive )
            return;

        UpdateTimeOfDay();
        RotateSun();
    }

    private void BattleStarted()
    {
        _battleActive = true;
    }

    private void BattleEnded()
    {
        _battleActive = false;
    }

    private void UpdateTimeOfDay(){
        CurrentTime = CurrentTime.AddSeconds( Time.deltaTime * _timeMultiplier );
        _timeText.text = CurrentTime.ToString( "hh:mm" );
    }

    private TimeSpan CalculateTimeDifference( TimeSpan fromTime, TimeSpan toTime ){
        TimeSpan difference = ( toTime - fromTime );

        if( difference.TotalSeconds < 0  )
            difference += TimeSpan.FromHours( 24 );
        
        return difference;
    }

    private void RotateSun(){
        float sunRotation;

        if( CurrentTime.TimeOfDay > _sunRiseTime && CurrentTime.TimeOfDay < _sunSetTime ){
            TimeSpan sunRiseToSunSetDuration = CalculateTimeDifference( _sunRiseTime, _sunSetTime );
            TimeSpan timeSinceSunRise = CalculateTimeDifference( _sunRiseTime, CurrentTime.TimeOfDay );

            double percentage = ( timeSinceSunRise.TotalMinutes / sunRiseToSunSetDuration.TotalMinutes );
            sunRotation = Mathf.Lerp( 0, 180, (float)percentage );

        } else {
            TimeSpan sunSetToSunRiseDuration = CalculateTimeDifference( _sunSetTime, _sunRiseTime );
            TimeSpan timeSinceSunSet = CalculateTimeDifference( _sunSetTime, CurrentTime.TimeOfDay );

            double percentage = ( timeSinceSunSet.TotalMinutes / sunSetToSunRiseDuration.TotalMinutes );
            sunRotation = Mathf.Lerp( 180, 360, (float)percentage );
        }

        _sun.transform.rotation = Quaternion.AngleAxis( sunRotation, new Vector3( 1f, 0.25f, 0 ) );
    }
}
