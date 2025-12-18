using System;
using UnityEngine;

public class PlaytimeTracker : MonoBehaviour, ISavable
{
    public static PlaytimeTracker Instance;
    private DateTime _saveStartTime;
    private DateTime _currentTime;
    public TimeSpan PlayTime { get; private set; }
    public TimeSpan LastSavePlaytime { get; private set; }
    private bool _saveStarted;
    public Action OnPlaytimeRequest;

    private void Awake(){
        OnPlaytimeRequest += UpdatePlayTime;
    }

    private void OnDisable(){
        OnPlaytimeRequest -= UpdatePlayTime;
    }
    
    private void Start(){
        Instance = this;

        if( _saveStarted )
            return;

        _saveStartTime = DateTime.Now;
        _saveStarted = true;
    }

    private void UpdatePlayTime(){
        PlayTime = GetPlaytime();
    }

    private TimeSpan GetPlaytime(){
        _currentTime = DateTime.Now;
        var playtime = _currentTime - _saveStartTime;
        
        return playtime;
    }

    public object CaptureState(){
        UpdatePlayTime();
        LastSavePlaytime = PlayTime;

        var saveData = new PlaytimeSaveData(){
            PlayTime = PlayTime,
            LastSavePlaytime = LastSavePlaytime,
            SaveStarted = _saveStarted,
        };

        return saveData;
    }

    public void RestoreState( object state ){
        var saveData = (PlaytimeSaveData)state;

        PlayTime            = saveData.PlayTime;
        LastSavePlaytime    = saveData.LastSavePlaytime;
        _saveStarted        = saveData.SaveStarted;
    }
}

[Serializable]
public class PlaytimeSaveData
{
    public TimeSpan PlayTime;
    public TimeSpan LastSavePlaytime;
    public bool SaveStarted;
}
