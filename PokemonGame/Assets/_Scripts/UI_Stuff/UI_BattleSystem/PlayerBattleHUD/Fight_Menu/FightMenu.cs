using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using NoxNoctisDev.StateMachine;

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
    [SerializeField] private List<MoveButton_Fight> _moveButtons;
    [SerializeField] private List<TextMeshProUGUI> _moveNameText, _ppText;

    public override void EnterState( PlayerBattleMenu owner ){
        gameObject.SetActive( true );
        Debug.Log( "EnterState: " + this );
        _battleMenu = owner;
        //--THIS SHOULD BE THE CURRENT UNIT YOU ARE SELECTING A MOVE FOR
        _activeUnit = _battleSystem.UnitInSelectionState;

        SetUpMoves( _activeUnit.Pokemon.ActiveMoves );

        _initialButton = move1button;
        if( _activeUnit.LastUsedMove != null )
            SetMemoryButton();

        StartCoroutine( SetInitialButton() );

        // BattleUIActions.OnFightMenuOpened?.Invoke(); //--These were probably for animations or whatever
    }

    public override void ReturnToState()
    {
        SelectMemoryButton();
    }

    public override void ExitState(){
        Debug.Log( "ExitState: " + this );
        // BattleUIActions.OnSubMenuClosed?.Invoke(); //--These were probably for animations or whatever
        // BattleUIActions.OnFightMenuClosed?.Invoke(); //--These were probably for animations or whatever
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

    private IEnumerator SetInitialButton()
    {
        yield return new WaitForSeconds( 0.15f );

        if( LastButton != null )
            SelectMemoryButton();
        else
            _initialButton.Select();

        BattleUIActions.OnFightMenuOpened?.Invoke();
    }

    private void SetMemoryButton()
    {
        if( _activeUnit.LastUsedMove != null )
        {
            for( int i = 0; i < _moveButtons.Count; i++ )
            {
                if( _moveButtons[i].AssignedMove == _activeUnit.LastUsedMove )
                    LastButton = _moveButtons[i].ThisButton;
            }
        }
        else
            LastButton = _initialButton;

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
