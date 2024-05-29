using System;
using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using TMPro;
using UnityEngine;

public class LearnMove_Pause : State<UI_PauseMenuStateMachine>, ILearnMoveContext
{
    private UI_PauseMenuStateMachine _pauseMenu;
    [SerializeField] private List<TextMeshProUGUI> _moveNames;
    [SerializeField] private List<LearnMoveButton_Pause> _moveButtons;
    private Pokemon _pokemon;
    public MoveSO NewMove { get; private set; }

    private Action<bool> _wasMoveLearned;


    public override void EnterState( UI_PauseMenuStateMachine owner ){
        Debug.Log( "EnterState: " + this );
        _pauseMenu = owner;

        //--Disable Battle Menu Buttons
        // _pauseMenu.DisableMenuButtons();

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
        _pauseMenu.StateMachine.Pop();
        _wasMoveLearned?.Invoke( true );
    }

    public void DontReplaceMove(){
        _pokemon.LearnedMoves.Add( new Move( NewMove ) );
        _pauseMenu.StateMachine.Pop();
        _wasMoveLearned?.Invoke( false );
    }
    
}
