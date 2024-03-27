using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using NoxNoctisDev.StateMachine;
using UnityEngine.EventSystems;

public class FightMenu : State<PlayerBattleMenu>
{
    [SerializeField] private BattleSystem _battleSystem;
    [SerializeField] private PlayerBattleMenu _battleMenu;
    public PlayerBattleMenu BattleMenu => _battleMenu;
    [SerializeField] private Button move1button, move2button, move3button, move4button;
    private Button _initialButton;
    public Button LastButton;
    [SerializeField] private BattleUnit _activeUnit;
    public BattleUnit ActiveUnit => _activeUnit;
    [SerializeField] private List<MoveButton> _moveButtons;
    [SerializeField] private List<TextMeshProUGUI> _moveNameText, _ppText;
    // [SerializeField] private Image _pokemonType_Image1, _pokemonType_Image2;

    public override void EnterState( PlayerBattleMenu owner ){
        gameObject.SetActive( true );
        Debug.Log( "EnterState: " + this );
        _battleMenu = owner;
        _activeUnit = _battleSystem.PlayerUnit;

        SetUpMoves( _activeUnit.Pokemon.Moves );

        _initialButton = move1button;

        StartCoroutine( SetInitialButton() );

        BattleUIActions.OnFightMenuOpened?.Invoke();
    }

    public override void ExitState(){
        BattleUIActions.OnSubMenuClosed?.Invoke();
        BattleUIActions.OnFightMenuClosed?.Invoke();
        gameObject.SetActive( false );
    }

    //--setupmoves will always be called on enable or enter state because you should
    //--never be able to access this menu (by enabling it via player battle menu submit on fight button)
    //--without the currently active pokemon set in the battle system

    public void SetUpMoves( List<MoveClass> moves ){
        SetMoveNames( moves );
        SetMoveButtons( moves );
    }

    private void SetMoveNames( List<MoveClass> moves ){
        for( int i = 0; i < moves.Count; i++ ){
            for( int moveTexti = 0; moveTexti < _moveNameText.Count; moveTexti++ ){
                if( i < _moveNameText.Count ){
                    _moveNameText[i].text = moves[i].moveBase.MoveName;
                    
                    if( moveTexti > i )
                        _moveNameText[moveTexti].text = "-";

                } else {
                        _moveNameText[i].text = "-";
                }
            }

            for( int moveTexti = 0; moveTexti < _ppText.Count; moveTexti++ ){
                if( i < _moveNameText.Count ){
                    _ppText[i].text = $"PP: {moves[i].moveBase.PP}";
                    
                    if( moveTexti > i )
                        _ppText[moveTexti].text = "PP: -";

                } else {
                    _ppText[i].text = "PP: -";
                }
            }
        }
    }

    private void SetMoveButtons( List<MoveClass> moves ){
        for( int i = 0; i < moves.Count; i++ ){
            _moveButtons[i].AssignedMove = moves[i];
        }
    }

    private IEnumerator SetInitialButton(){
        yield return new WaitForSeconds( 0.15f );
        _initialButton.Select();
        BattleUIActions.OnFightMenuOpened?.Invoke();
    }

}
