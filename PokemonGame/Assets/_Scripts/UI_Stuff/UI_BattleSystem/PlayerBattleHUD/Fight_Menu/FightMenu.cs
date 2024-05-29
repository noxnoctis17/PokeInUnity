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
    public Button LastButton { get; private set; }
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

        SetUpMoves( _activeUnit.Pokemon.ActiveMoves );

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

    public void SetUpMoves( List<Move> moves ){
        SetMoveNames( moves );
        SetMoveButtons( moves );
    }

    private void SetMoveNames( List<Move> moves ){
        for( int i = 0; i < moves.Count; i++ ){
            for( int moveTexti = 0; moveTexti < _moveNameText.Count; moveTexti++ ){
                if( i < _moveNameText.Count ){
                    _moveNameText[i].text = moves[i].MoveSO.Name;
                    
                    if( moveTexti > i )
                        _moveNameText[moveTexti].text = "-";

                } else {
                        _moveNameText[i].text = "-";
                }
            }

            for( int moveTexti = 0; moveTexti < _ppText.Count; moveTexti++ ){
                if( i < _moveNameText.Count ){
                    _ppText[i].text = $"PP: {moves[i].PP}/{moves[i].MoveSO.PP}";
                    
                    if( moveTexti > i )
                        _ppText[moveTexti].text = "PP: -";

                } else {
                    _ppText[i].text = "PP: -";
                }
            }
        }
    }

    private void SetMoveButtons( List<Move> moves ){
        for( int m = 0; m < moves.Count; m++ ){
            for( int b = 0; b < _moveButtons.Count; b++ ){
                _moveButtons[m].GetComponent<Button>().interactable = true;
                _moveButtons[m].AssignedMove = moves[m];

                if( b > m )
                    _moveButtons[b].GetComponent<Button>().interactable = false;
            }
        }
    }

    private IEnumerator SetInitialButton(){
        yield return new WaitForSeconds( 0.15f );

        if( LastButton != null )
            SelectMemoryButton();
        else{
            SetMemoryButton( _initialButton );
        }

        BattleUIActions.OnFightMenuOpened?.Invoke();
    }

    public void SetMemoryButton( Button lastButton ){
        LastButton = lastButton;
        SelectMemoryButton();
    }

    private void SelectMemoryButton(){
        LastButton.Select();
    }

    public void ClearMemoryButton(){
        LastButton = null;
        _initialButton.Select();
    }

}
