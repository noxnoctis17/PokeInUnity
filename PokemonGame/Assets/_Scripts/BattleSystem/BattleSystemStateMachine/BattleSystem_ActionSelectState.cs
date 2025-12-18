using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class BattleSystem_ActionSelectState : State<BattleSystem>
{
    private BattleSystem _battleSystem;
    private int _commands;
    private Coroutine _awaitCommands;

    public override void EnterState( BattleSystem owner ){
        _battleSystem = owner;
        BattleSystem.OnCommandAdded += IncreaseCommandCount;
        _commands = 0;

        _battleSystem.SetStateEnum( BattleStateEnum.ActionSelect );
        _battleSystem.PlayerBattleMenu.OnUnpauseState?.Invoke();
        _battleSystem.PlayerBattleMenu.EnableMenuButtons();
        
        _awaitCommands = StartCoroutine( AwaitActionSelections() );
    }

    public override void ExitState(){
        BattleSystem.OnCommandAdded -= IncreaseCommandCount;
        StopCoroutine( _awaitCommands );
        _commands = 0;
        _battleSystem.PlayerBattleMenu.OnPauseState?.Invoke();
        _battleSystem.PlayerBattleMenu.DisableMenuButtons();
    }

    public override void PauseState(){
        BattleSystem.OnCommandAdded -= IncreaseCommandCount;
        StopCoroutine( _awaitCommands );
        _commands = 0;
        _battleSystem.PlayerBattleMenu.OnPauseState?.Invoke();
        _battleSystem.PlayerBattleMenu.DisableMenuButtons();
    }

    public override void ReturnToState(){
        BattleSystem.OnCommandAdded += IncreaseCommandCount;
        _battleSystem.SetStateEnum( BattleStateEnum.ActionSelect );
        _commands = 0;
        _battleSystem.PlayerBattleMenu.OnUnpauseState?.Invoke();
        _battleSystem.PlayerBattleMenu.EnableMenuButtons();

        _awaitCommands = StartCoroutine( AwaitActionSelections() );
    }

    private void IncreaseCommandCount()
    {

        if( _commands < _battleSystem.UnitsInBattle )
            _commands++;

    }

    private IEnumerator AwaitActionSelections()
    {
        yield return new WaitUntil( () => _commands == _battleSystem.ActivePlayerUnitsCount );
        _battleSystem.BeginAIActionSelect();
        yield return null;
    }
}
