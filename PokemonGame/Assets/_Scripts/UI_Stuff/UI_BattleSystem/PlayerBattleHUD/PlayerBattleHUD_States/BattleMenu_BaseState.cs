using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using NoxNoctisDev.StateMachine;

public class BattleMenu_BaseState : State<PlayerBattleMenu>
{
    private PlayerBattleMenu _battleMenu;
    private BattleUIActions _battleUIActions;
    private Button _activeButton;
    private Button _memorizeButton;
    private Button _previousButton;
    private PlayerInput _playerInput;
    private float _cardRotationAmount;
    private bool _isNavigating;

    public override void EnterState( PlayerBattleMenu owner ){
        Debug.Log( "Entering Base Menu State" );
        _battleMenu = owner;

        //--Reference Assignments
        _playerInput = _battleMenu.PlayerInput;
        _battleUIActions = _battleMenu.BUIActions;

        //--Events
        _playerInput.UIBattle.Navigate.performed += OnNavigate;
        _battleUIActions.OnButtonSelected += SetActiveButton;

        //--Select Initial Button
        _cardRotationAmount = 15f;
        StartCoroutine( SelectInitialButton() );
    }

    public override void UpdateState(){
        if( _isNavigating && _playerInput.UI.Navigate.ReadValue<Vector2>().x == 0 ){
            _isNavigating = false;
        }
    }

    public override void ExitState(){
        Debug.Log( "Exit Base Menu State" );
        //--Events
        _playerInput.UIBattle.Navigate.performed -= OnNavigate;
        _battleUIActions.OnButtonSelected -= SetActiveButton;

        //--Clear Selected Button
        ClearSelectedButton();

        //--Disable Menu Buttons
        _battleMenu.DisableMenuButtons();
    }

    public override void PauseState(){
        Debug.Log( "PauseState from BattleMenu_BaseState" );
        //--Events
        _playerInput.UIBattle.Navigate.performed -= OnNavigate;
        _battleUIActions.OnButtonSelected -= SetActiveButton;

        //--Clear Selected Button
        ClearSelectedButton();

        //--Disable Menu Buttons
        _battleMenu.DisableMenuButtons();
    }

    public override void ReturnToState(){
        Debug.Log( "Returning to BattleMenu_BaseState" );
        //--Events
        _playerInput.UIBattle.Navigate.performed += OnNavigate;
        _battleUIActions.OnButtonSelected += SetActiveButton;

        //--Enable Menu Buttons
        _battleMenu.EnableMenuButtons();

        //--Select Memorize Button
        SelectMemorizeButton();
    }

    private IEnumerator SelectInitialButton(){
        //--Disable Player Controls Immediately
        PlayerReferences.Instance.PlayerController.DisableCharacterControls();

        //--Give everything some time to assign and set before we select initial button, this will wait for anims eventually
        yield return new WaitForSeconds( 2f );

        //--Enable UI Controls After Delay
        // PlayerReferences.Instance.PlayerController.EnableUI();
        PlayerReferences.Instance.PlayerController.EnableBattleControls();

        //--Enable Menu Buttons
        _battleMenu.EnableMenuButtons();

        //--Select Initial Button
        if( _memorizeButton == null ){
            _activeButton = _battleMenu.FightButton;
            _memorizeButton = _activeButton;
        }else
            _activeButton = _memorizeButton;

        if( _previousButton == null )
            _previousButton = _battleMenu.RunButton;

        _activeButton.Select();
    }

    private void SelectMemorizeButton(){
        // Debug.Log( "EventSystem in battlesystem is: " + _battleMenu.BattleSystem.EventSystem.enabled );
        // Debug.Log( "memorize button is: " + _memorizeButton );
        if( _memorizeButton == null )
            _memorizeButton = _battleMenu.FightButton;

        _memorizeButton.Select();
    }

    private void SetActiveButton( Button button ){
        var activeScale = new Vector3( 1.1f, 1.1f, 1.1f );
        var buttonRect = button.GetComponent<RectTransform>();

        _previousButton = _activeButton;
        _activeButton = button;
        _memorizeButton = _activeButton;

        buttonRect.localScale = activeScale;
    }

    private void ClearSelectedButton(){
        _battleMenu.BattleSystem.EventSystem.SetSelectedGameObject( null );
    }

    private void OnNavigate( InputAction.CallbackContext context ){
        Vector2 direction = context.ReadValue<Vector2>();

        //--Player moved Up through the menu 
        if( direction.y > 0 ){
            _isNavigating = true;
            RightcreasePositions();
        }

        //--Player moved Down through the menu
        if( direction.y < 0 ){
            _isNavigating = true;
            LeftcreasePositions();
        }

        //--Player moved Right through the menu
        if( direction.x > 0 ){
            _isNavigating = true;
            RightcreasePositions();
        }

        //--Player moved Left through the menu
        if( direction.x < 0 ){
            _isNavigating = true;
            LeftcreasePositions();
        }
    }

    //--Increase position value
    private void LeftcreasePositions(){
        foreach( Button button in _battleMenu.Buttons ){
            var buttonRect = button.GetComponent<RectTransform>();
            var newRotation = buttonRect.rotation * Quaternion.Euler( 0f, 0f, -_cardRotationAmount );

            buttonRect.rotation = newRotation;

            if( button == _activeButton ){
                var setRotation = Quaternion.Euler( 0f, 0f, 45f );
                var inactiveScale = new Vector3( 1f, 1f, 1f );

                buttonRect.rotation = setRotation;
                buttonRect.localScale = inactiveScale;
                
                buttonRect.SetAsFirstSibling();
            }
        }
    }

    //--Decrease position value
    private void RightcreasePositions(){
        foreach( Button button in _battleMenu.Buttons ){
            var buttonRect = button.GetComponent<RectTransform>();
            var newRotation = buttonRect.rotation * Quaternion.Euler( 0f, 0f, _cardRotationAmount );
            var inactiveScale = new Vector3( 1f, 1f, 1f );
            
            buttonRect.rotation = newRotation;

            if( button == GetBottomCard() ){
                var setRotation = Quaternion.Euler( 0f, 0f, 0f );
                buttonRect.rotation = setRotation;

                buttonRect.SetAsLastSibling();
            }
            else{
                buttonRect.localScale = inactiveScale;
            }
        }
    }

    private Button GetBottomCard(){
        Button card;

        for( int i = 0; i < _battleMenu.Buttons.Length; i++ ){
            if( _activeButton == _battleMenu.Buttons[i] ){
                if( i == 3 ){
                    card = _battleMenu.Buttons[0];
                    return card;
                }
                else{
                    card = _battleMenu.Buttons[ i + 1 ];
                    return card;
                }
            }
        }

        return default;
    }
}
