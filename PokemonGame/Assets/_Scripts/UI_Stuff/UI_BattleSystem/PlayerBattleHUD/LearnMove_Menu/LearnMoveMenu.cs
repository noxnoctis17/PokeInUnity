using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;
using NoxNoctisDev.StateMachine;
using Unity.VisualScripting;

public class LearnMoveMenu : State<PlayerBattleMenu>
{
    private PlayerBattleMenu _battleMenu;
    [SerializeField] private BattleSystem _battleSystem;
    [SerializeField] private List<TextMeshProUGUI> _moveNames;
    [SerializeField] private List<LearnMoveButton> _moveButtons;
    public PlayerBattleMenu BattleMenu => _battleMenu;
    private PokemonClass _pokemon;
    private MoveBaseSO _newMove;
    public Action<MoveBaseSO> OnReplaceMove;
    public Action OnDontReplaceMove;
    public bool ReplacedMove { get; private set; }

    //--how could you forget that fucking there will ALWAYS be 5 move buttons here because
    //--if this menu even needs to come up in the first place, you already have 4 moves, and the 5th
    //--button is for the new move you're trying to learn!! you will never have more or less than 5!!!!
    //--clown!!!!

    public override void EnterState( PlayerBattleMenu owner ){
        Debug.Log( "EnterState: " + this );
        _battleMenu = owner;

        OnReplaceMove += ReplaceMove;
        OnDontReplaceMove += SetReplacedMoveFalse;

        //--Disable Battle Menu Buttons
        _battleMenu.DisableMenuButtons();
    }

    public override void ExitState(){
        gameObject.SetActive( false );
    }

    private IEnumerator SetInitialButton(){
        yield return new WaitForSeconds( 0.15f );
        _moveButtons[0].ThisButton.Select();
    }

    public void Setup( PokemonClass pokemon, List<MoveBaseSO> currentMoves, MoveBaseSO newMove ){
        for( int i = 0; i < _moveNames.Count - 1; i++ ){
            _moveNames[i].text = currentMoves[i].MoveName;
            _moveButtons[i].Setup( _battleSystem, this, currentMoves[i] );
        }

        _moveNames[currentMoves.Count].text = newMove.MoveName;
        _moveButtons[currentMoves.Count].Setup( _battleSystem, this, newMove );
        _pokemon = pokemon;
        _newMove = newMove;

        StartCoroutine( SetInitialButton() );
    }

    private void ReplaceMove( MoveBaseSO replacedMove ){
        ReplacedMove = true;
        _pokemon.ReplaceWithNewMove( replacedMove, _newMove );
        _battleMenu.BattleMenuStateMachine.Pop();
    }

    private void SetReplacedMoveFalse(){
        ReplacedMove = false;
        _battleMenu.BattleMenuStateMachine.Pop();
    }
}
