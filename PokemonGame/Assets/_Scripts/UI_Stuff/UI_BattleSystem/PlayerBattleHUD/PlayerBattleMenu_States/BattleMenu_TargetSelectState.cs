using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class BattleMenu_TargetSelectState : State<PlayerBattleMenu>
{
    public PlayerBattleMenu PlayerBattleMenu { get; private set; }
    private BattleUnit _attacker;
    private List<BattleUnit> _targetUnits;
    public BattleUnit ChosenTarget { get; private set; }
    private Move _chosenMove;
    [SerializeField] private List<TargetSelect_Button> _targetButtons;
    private Button _initialButton;
    public Button LastButton { get; private set; }

    public override void EnterState( PlayerBattleMenu owner )
    {
        PlayerBattleMenu = owner;
        _attacker = PlayerBattleMenu.Attacker;
        _chosenMove = PlayerBattleMenu.ChosenMove;
        _targetUnits = new();
        gameObject.SetActive( true );
        SetupButtons();
        _initialButton = _targetButtons[0].ThisButton;
        StartCoroutine( SetInitialButton() );
        PlayerBattleMenu.DisableMenuButtons();
    }

    public override void ExitState()
    {
        _attacker = null;
        _chosenMove = null;
        _targetUnits = null;
        gameObject.SetActive( false );
        PlayerBattleMenu.EnableMenuButtons();
    }

    private void SetupButtons()
    {
        //--Buttons 0 and 1 will be the top two, left to right. These are the opposing team buttons in doubles
        //--In doubles, the positions are flipped from the player - so the left unit is unit 1, and the right unit is unit 0
        //--We will assign enemy unit 1 to button 0, and enemy unit 0 to button 1. This is because we read left to right like adults.
        //--The player units are buttons 2 and 3, which will align with the unit indicies correctly.

        //--We'll add the enemy units first, so they're indicies 0, 1
        var enemyUnits = PlayerBattleMenu.BattleSystem.EnemyUnits;
        for( int i = 0; i < enemyUnits.Count; i++ )
        {
            _targetUnits.Add( enemyUnits[i] );
        }

        //--Then we'll add the player units so they're indicies 2, 3
        var playerUnits = PlayerBattleMenu.BattleSystem.PlayerUnits;
        for( int i = 0; i < playerUnits.Count; i++)
        {
            _targetUnits.Add( playerUnits[i] );
        }

        //--This will likely have to be handled differently once we expand the enemy units passed 2, but for now, this is fine.

        //--Next, we'll "hard code" the buttons as described above.
        _targetButtons[0].Setup( _targetUnits[1], _attacker, _chosenMove );
        _targetButtons[1].Setup( _targetUnits[0], _attacker, _chosenMove );
        _targetButtons[2].Setup( _targetUnits[2], _attacker, _chosenMove );
        _targetButtons[3].Setup( _targetUnits[3], _attacker, _chosenMove );
    }

    private IEnumerator SetInitialButton(){
        yield return new WaitForSeconds( 0.15f );

        if( LastButton != null )
            SelectMemoryButton();
        else{
            SetMemoryButton( _initialButton );
        }
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
