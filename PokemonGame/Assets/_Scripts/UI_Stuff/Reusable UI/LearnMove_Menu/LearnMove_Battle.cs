using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;
using NoxNoctisDev.StateMachine;

public class LearnMove_Battle : State<PlayerBattleMenu>, ILearnMoveContext
{
    private PlayerBattleMenu _battleMenu;
    [SerializeField] private List<TextMeshProUGUI> _moveNames;
    [SerializeField] private List<LearnMoveButton_Battle> _moveButtons;
    public PlayerBattleMenu BattleMenu => _battleMenu;
    private Pokemon _pokemon;
    public MoveSO NewMove { get; private set; }
    private Action<bool> _wasMoveLearned;

    //--how could you forget that fucking there will ALWAYS be 5 move buttons here because
    //--if this menu even needs to come up in the first place, you already have 4 moves, and the 5th
    //--button is for the new move you're trying to learn!! you will never have more or less than 5!!!!
    //--clown!!!!

    public override void EnterState( PlayerBattleMenu owner ){
        Debug.Log( "EnterState: " + this );
        _battleMenu = owner;

        //--Disable Battle Menu Buttons
        _battleMenu.DisableMenuButtons();

        //--Open Menu
        gameObject.SetActive( true );

        //--Select Initial Button
        StartCoroutine( SetInitialButton() );
    }

    public override void ExitState(){
        gameObject.SetActive( false );
    }

    public void Setup( Pokemon pokemon, List<MoveSO> currentMoves, MoveSO newMove, Action<bool> wasMoveLearned ){
        for( int i = 0; i < _moveNames.Count - 1; i++ ){
            _moveNames[i].text = currentMoves[i].Name;
            _moveButtons[i].Setup( this, currentMoves[i] );
        }

        _moveNames[currentMoves.Count].text = newMove.Name;
        _moveButtons[currentMoves.Count].Setup( this, newMove );
        _pokemon = pokemon;
        NewMove = newMove;
        _wasMoveLearned = wasMoveLearned;
    }

    private IEnumerator SetInitialButton(){
        yield return new WaitForSeconds( 0.15f );
        _moveButtons[0].ThisButton.Select();
    }

    public void ReplaceMove( MoveSO replacedMove ){
        _pokemon.ReplaceWithNewMove( replacedMove, NewMove );
        _battleMenu.StateMachine.Pop();
        _wasMoveLearned?.Invoke( true );
    }

    public void DontReplaceMove(){
        _pokemon.LearnedMoves.Add( new Move( NewMove ) );
        _battleMenu.StateMachine.Pop();
        _wasMoveLearned?.Invoke( false );
    }
}
