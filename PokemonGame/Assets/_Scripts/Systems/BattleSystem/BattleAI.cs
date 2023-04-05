using UnityEngine;
using System;

public class BattleAI : MonoBehaviour
{
    public Action OnPlayerCommandSelect;
    private BattleSystem _battleSystem;
    private BattleUnit _battleUnit;
    private PokemonClass _pokemon;
    private MoveClass _move;

    private void Start(){
        OnPlayerCommandSelect += ChooseCommand;
    }

    public void SetupAI( BattleSystem battleSystem, BattleUnit battleUnit ){
        _battleSystem = battleSystem;
        _battleUnit = battleUnit;
        _pokemon = _battleUnit.Pokemon;
        Debug.Log( _pokemon.PokeSO.pName + " obj name: " + name );
    }

    private void ChooseCommand(){
        //blah blah logic to decide whether it should use an item, switch pokemon, or choose a move
        ChooseMoveCommand();

    }

    private void ChooseMoveCommand(){
        Debug.Log( "choose move command" + " obj name: " + name );
        Debug.Log( _pokemon + " obj name: " + name );
        _move = _pokemon.GetRandomMove();
        _battleSystem.SetEnemyMoveCommand( _battleUnit, _move );

    }

}
