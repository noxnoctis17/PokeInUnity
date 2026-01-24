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

    private void IncreaseCommandCount()
    {
        Debug.Log( $"[Action Select][Move Command] Command entred by: {_battleSystem.UnitInSelectionState.Pokemon.NickName}, Count Before Command Increase: {_commands}" );
        if( _commands < _battleSystem.ActivePlayerUnitsCount )
            _commands++;

        Debug.Log( $"[Action Select][Move Command] Command entred by: {_battleSystem.UnitInSelectionState.Pokemon.NickName}, Count After Increase: {_commands}" );
    }

    private IEnumerator AwaitActionSelections()
    {
        Debug.Log( $"[Action Select][Move Command] Awaiting Command amount of: {_battleSystem.ActivePlayerUnitsCount + _battleSystem.ActiveSecondPlayerUnitsCount}. Commands count: {_commands}" );
        yield return new WaitUntil( () => _commands == _battleSystem.ActivePlayerUnitsCount + _battleSystem.ActiveSecondPlayerUnitsCount );
        Debug.Log( $"[Action Select][Move Command] Reached Command amount of: {_battleSystem.ActivePlayerUnitsCount + _battleSystem.ActiveSecondPlayerUnitsCount}. Commands count: {_commands}" );

        if( _battleSystem.BattleType == BattleType.PvP_Singles || _battleSystem.BattleType == BattleType.PvP_Doubles )
            _battleSystem.BeginCommandQueueState();
        else
            _battleSystem.BeginAIActionSelect();

        yield return null;
    }
}
