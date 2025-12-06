using UnityEngine;
using System;

public class BattleAI : MonoBehaviour
{
    public Action OnAITurn;
    private BattleSystem _battleSystem;
    private BattleUnit _battleUnit;
    private Pokemon _pokemon;
    private Move _move;

    private void OnEnable(){
        OnAITurn += ChooseCommand;
    }

    private void OnDisable() //--if ai decisions are funky, get rid of this and change OnEnable() back to Start(). should be fine, though --11/25/25
    {
        OnAITurn -= ChooseCommand;
    }

    public void SetupAI( BattleSystem battleSystem, BattleUnit battleUnit ){
        _battleSystem = battleSystem;
        _battleUnit = battleUnit;
        _pokemon = _battleUnit.Pokemon;
    }

    private void ChooseCommand(){
        if( _battleUnit.Pokemon.SevereStatus?.ID == StatusConditionID.FNT || _battleUnit.Pokemon.CurrentHP == 0 )
            return;
            
        int target = 0;
        if( _battleSystem.BattleType == BattleType.TrainerDoubles )
            target = UnityEngine.Random.Range( 0, _battleSystem.UnitsInBattle );
    
        ChooseMoveCommand( _battleSystem.PlayerUnits[target] );

    }

    private void ChooseMoveCommand( BattleUnit target ){
        Debug.Log( "AI ChooseMoveCommand()" );
        _move = _pokemon.GetRandomMove();
        _battleSystem.SetEnemyMoveCommand( _battleUnit, target, _move );
    }

}
