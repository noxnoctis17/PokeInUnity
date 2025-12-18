using System;
using NoxNoctisDev.StateMachine;
using TMPro;
using UnityEngine;

public class UI_SaveScreen : State<UI_PauseMenuStateMachine>
{
    private UI_PauseMenuStateMachine _pauseMenuStateMachine;
    private UI_PauseMenu _pauseMenu;
    [SerializeField] private SaveScreenButton _saveButton;
    [SerializeField] private TextMeshProUGUI _playtimeText;
    public Action OnSaveMade;

    public override void EnterState( UI_PauseMenuStateMachine owner ){
        _pauseMenuStateMachine = owner;
        var lastsaveTime = PlaytimeTracker.Instance.LastSavePlaytime;
        OnSaveMade += UpdateSaveInfo;

        if( lastsaveTime == null )
            _playtimeText.text = $"Playtime: 0h0m0s";
        else
            _playtimeText.text = $"Playtime: {lastsaveTime:h\\hm\\ms\\s}";

        gameObject.SetActive( true );

        _saveButton.Setup( _pauseMenuStateMachine, this );
    }

    public override void ReturnToState(){
        OnSaveMade += UpdateSaveInfo;
    }

    public override void PauseState(){
        OnSaveMade -= UpdateSaveInfo;
    }

    public override void ExitState(){
        OnSaveMade -= UpdateSaveInfo;
        gameObject.SetActive( false );
    }

    private void UpdateSaveInfo(){
        var lastsaveTime = PlaytimeTracker.Instance.LastSavePlaytime;
        _playtimeText.text = $"Playtime: {lastsaveTime:h\\hm\\ms\\s}";
    }

}
