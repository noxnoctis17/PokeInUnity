using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using NoxNoctisDev.StateMachine;
using UnityEngine;
using System;

public class BattleMenu_TargetSelectState : State<PlayerBattleMenu>
{
    public PlayerBattleMenu PlayerBattleMenu { get; private set; }
    [SerializeField] private TargetSelect_Multiple _multiSelect;
    private List<BattleUnit> _targetUnits;
    public BattleUnit ChosenTarget { get; private set; }
    [SerializeField] private List<TargetSelect_Button> _targetButtons;
    private Button _initialButton;
    public Button LastButton { get; private set; }
    public BattleUnit Attacker { get; private set; }
    public Move ChosenMove { get; private set; }

    public override void EnterState( PlayerBattleMenu owner )
    {
        PlayerBattleMenu = owner;
        Attacker = PlayerBattleMenu.Attacker;
        ChosenMove = PlayerBattleMenu.ChosenMove;
        _targetUnits = new();
        gameObject.SetActive( true );
        SetScreen();
        PlayerBattleMenu.DisableMenuButtons();
    }

    public override void PauseState()
    {
        base.PauseState();
    }

    public override void ExitState()
    {
        Attacker = null;
        ChosenMove = null;
        _targetUnits = null;
        _multiSelect.gameObject.SetActive( false );
        gameObject.SetActive( false );
        PlayerBattleMenu.EnableMenuButtons();
    }

    private void SetScreen()
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

        SetButtons( ChosenMove.MoveTarget );
    }

    private void SetButtons( MoveTarget targetType )
    {
        //--This will likely have to be handled differently once we expand the enemy units passed 2, but for now, this is fine.
        //--Next, we'll "hard code" the buttons as described above.
        _targetButtons[0].Setup( _targetUnits[1], Attacker, ChosenMove );
        _targetButtons[1].Setup( _targetUnits[0], Attacker, ChosenMove );
        _targetButtons[2].Setup( _targetUnits[2], Attacker, ChosenMove );
        _targetButtons[3].Setup( _targetUnits[3], Attacker, ChosenMove );

        switch( targetType )
        {
            //--Free selection of targeting. "Enemy" technically also allows for side targeting an ally with a damaging move or negative status move.
            case MoveTarget.Enemy:
                SetEnemy();
            break;

            case MoveTarget.Self:
                //--We should auto select self unit and disable all other unit buttons.
                SetSelf();
            break;

            case MoveTarget.OpposingSide:
                //--We will fake set both opposing units and disable both ally units. We need to create a new TargetSelectMultiple_Button class to receive and send the appropriate units.
                //--This button should be invisible and have no fanfare whatsoever
                SetOpposingSide();
            break;

            case MoveTarget.AllAdjacent:
                //--This should highlight all units other than the user and pass them to the Multiple button.
                SetAllAdjacent();
            break;

            case MoveTarget.Ally:
                //--This should disable all units except the ally unit
                SetAlly();
            break;

            case MoveTarget.AllySide:
                //--This should fake select both ally units and disable both opposing units. opposite of Opposing Side.
                SetAllySide();
            break;

            case MoveTarget.All:
                //--This should fake select all units.
                SetAll();
            break;

            case MoveTarget.AllField:
                SetAllField();
            break;
        }
    }

    private int GetUserIndex()
    {
        int index = 0;
        for( int i = 0; i < _targetButtons.Count; i++ )
        {
            if( _targetButtons[i].AssignedUnit == Attacker )
                index = i;
        }

        return index;
    }

    private TargetSelect_Button GetUserButton( int userIndex )
    {
        for( int i = 0; i < _targetButtons.Count; i++ )
        {
            if( i == userIndex )
                return _targetButtons[i];
            else
                continue;
        }

        Debug.LogError( $"Index has no match in Target Buttons!" );
        return default;
    }

    private void SetEnemy()
    {
        foreach( var button in _targetButtons )
            button.SetInteractable( true );

        _initialButton = _targetButtons[0].ThisButton;
        StartCoroutine( SetInitialButton() );
    }

    private void SetSelf()
    {
        for( int i = 0; i < _targetButtons.Count; i++ )
        {
            if( _targetButtons[i].AssignedUnit == Attacker )
            {
                _targetButtons[i].SetInteractable( true );
                _targetButtons[i].ThisButton.Select();
            }
            else
                _targetButtons[i].SetInteractable( false );
        }
    }

    private void SetOpposingSide()
    {
        //--Hard coding this like this only works for player vs cpu. player vs player will break.
        List<TargetSelect_Button> opposingButtons = new()
        {
            _targetButtons[1],
            _targetButtons[0]
        };

        List<TargetSelect_Button> disableButtons = new()
        {
            _targetButtons[2],
            _targetButtons[3]
        };

        var user = GetUserButton( GetUserIndex() );
        _multiSelect.Init( ChosenMove, opposingButtons, disableButtons, user, opposingButtons );
    }

    private void SetAllAdjacent()
    {
        Debug.Log( $"[Target Select][Set All Adjacent] Attacker is: {Attacker.Pokemon.NickName}, using: {ChosenMove.MoveSO.Name}" );
        List<TargetSelect_Button> allAdjacent = new();
        List<TargetSelect_Button> disableButtons = new();
        int userIndex = GetUserIndex();

        Debug.Log( $"[Target Select][Set All Adjacent] User Index is: {userIndex}, who is: {_targetButtons[userIndex].AssignedUnit.Pokemon.NickName}" );

        foreach( var button in _targetButtons )
        {
            if( button.AssignedUnit == _targetButtons[userIndex].AssignedUnit )
                disableButtons.Add( button );
            else if( button.AssignedUnit != _targetButtons[userIndex].AssignedUnit )
                allAdjacent.Add( button );
        }

        var user = GetUserButton( userIndex );
        _multiSelect.Init( ChosenMove, allAdjacent, disableButtons, user, allAdjacent );
    }

    private void SetAlly()
    {
        TargetSelect_Button ally;
        TargetSelect_Button user = _targetButtons[GetUserIndex()];
        List<TargetSelect_Button> disableButtons = new();

        //--There must be a better way to do this...
        if( user == _targetButtons[0] )
            ally = _targetButtons[1];
        else if( user == _targetButtons[1] )
            ally = _targetButtons[0];
        else if( user == _targetButtons[2] )
            ally = _targetButtons[3];
        else
            ally = _targetButtons[2];

        foreach( var button in disableButtons )
        {
            button.SetInteractable( false );
        }

        ally.SetInteractable( true );
        ally.ThisButton.Select();
    }

    private void SetAllySide()
    {
        //--Hard coding this like this only works for player vs cpu. player vs player will break.
        List<TargetSelect_Button> disableButtons = new()
        {
            _targetButtons[1],
            _targetButtons[0]
        };

        List<TargetSelect_Button> allyButtons = new()
        {
            _targetButtons[2],
            _targetButtons[3]
        };

        var user = GetUserButton( GetUserIndex() );
        _multiSelect.Init( ChosenMove, allyButtons, disableButtons, user, allyButtons );
    }

    private void SetAll()
    {
        //--We have to do some differentiating here. "All" is actually misleading.
        //--In the case of something like rain dance or trick room, it actually runs the move
        //--for every single target on the field. That's actually incorrect - we should make it so
        //--status moves that effect the FIELD, and thus "everyone" actually only target the user.
        var user = GetUserButton( GetUserIndex() );
        _multiSelect.Init( ChosenMove, _targetButtons, null, user, _targetButtons );
    }

    private void SetAllField()
    {
        var user = GetUserButton( GetUserIndex() );
        List<TargetSelect_Button> targets = new() { user };
        _multiSelect.Init( ChosenMove, _targetButtons, null, user, targets );
    }

    private IEnumerator SetInitialButton()
    {
        yield return new WaitForSeconds( 0.15f );

        if( LastButton != null )
            SelectMemoryButton();
        else
            SetMemoryButton( _initialButton );
    }

    public void SetMemoryButton( Button lastButton )
    {
        LastButton = lastButton;
        SelectMemoryButton();
    }

    private void SelectMemoryButton()
    {
        LastButton.Select();
    }

    public void ClearMemoryButton()
    {
        LastButton = null;
        _initialButton.Select();
    }

    public void PopTargetSelectState()
    {
        Debug.Log( $"[Target Select] Popping Target Select State!" );
        PlayerBattleMenu.PopState();
    }

}
