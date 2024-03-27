using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using NoxNoctisDev.StateMachine;
using UnityEngine.InputSystem.Controls;

public class BattleMenu_BaseState : State<PlayerBattleMenu>
{
    private PlayerBattleMenu _battleMenuSM;
    private BattleUIActions _battleUIActions;
    private Button _activeButton;
    private Button _memorizeButton;
    private Button _previousButton;
    private PlayerInput _playerInput;
    private float _cardRotationAmount;
    private bool _isNavigating;

    public override void EnterState( PlayerBattleMenu owner ){
        Debug.Log( "Entering Base Menu State" );
        _battleMenuSM = owner;

        //--Reference Assignments
        _playerInput = _battleMenuSM.PlayerInput;
        _battleUIActions = _battleMenuSM.BUIActions;

        //--Events
        _playerInput.UI.Navigate.performed += OnNavigate;
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
        _playerInput.UI.Navigate.performed -= OnNavigate;
        _battleUIActions.OnButtonSelected -= SetActiveButton;
    }

    public override void PauseState(){
        Debug.Log( "Paused Base Menu State" );
        //--Events
        _playerInput.UI.Navigate.performed -= OnNavigate;
        _battleUIActions.OnButtonSelected -= SetActiveButton;
    }

    public override void ReturnToState(){
        //--Events
        _playerInput.UI.Navigate.performed += OnNavigate;
        _battleUIActions.OnButtonSelected += SetActiveButton;

        //--Select Memorize Button
        SelectMemorizeButton();
    }

    private IEnumerator SelectInitialButton(){
        //--Disable Player Controls Immediately
        PlayerReferences.Instance.DisableCharacterControls();

        //--Give everything some time to assign and set before we select initial button, this will wait for anims eventually
        yield return new WaitForSeconds( 2f );

        //--Enable UI Controls After Delay
        PlayerReferences.Instance.EnableUI();

        //--Select Initial Button
        if( _memorizeButton == null ){
            _activeButton = _battleMenuSM.FightButton;
            _memorizeButton = _activeButton;
        }else
            _activeButton = _memorizeButton;

        if( _previousButton == null )
            _previousButton = _battleMenuSM.RunButton;

        _activeButton.Select();
    }

    private void SelectMemorizeButton(){
        if( _memorizeButton == null )
            _memorizeButton = _battleMenuSM.FightButton;

        _memorizeButton.Select();
    }

    private void SetActiveButton( Button button ){
        _previousButton = _activeButton;
        _activeButton = button;
        _memorizeButton = _activeButton;
    }

    private void OnNavigate( InputAction.CallbackContext context ){
        Vector2 direction = context.ReadValue<Vector2>();

        // if( _isNavigating )
        //     return;

        // if( _isNavigating && direction.x == 0 )
        //     _isNavigating = false;

        //--Player moved through the menu to the right via dpad or keyboard
        if( direction.x > 0 ){
            _isNavigating = true;
            RightcreasePositions();
        }

        //--Player moved through the menu to the left via dpad or keyboard
        if( direction.x < 0 ){
            _isNavigating = true;
            LeftcreasePositions();
        }
    }

    //--Increase position value
    private void LeftcreasePositions(){
        foreach( Button button in _battleMenuSM.Buttons ){
            var newRotation = button.GetComponent<RectTransform>().rotation * Quaternion.Euler( 0f, 0f, -_cardRotationAmount );
            button.GetComponent<RectTransform>().rotation = newRotation;

            if( button == _activeButton ){
                var setRotation = Quaternion.Euler( 0f, 0f, 45f );
                button.GetComponent<RectTransform>().rotation = setRotation;
                button.GetComponent<RectTransform>().SetAsFirstSibling();
            }
        }
    }

    //--Decrease position value
    private void RightcreasePositions(){
        foreach( Button button in _battleMenuSM.Buttons ){
            var newRotation = button.GetComponent<RectTransform>().rotation * Quaternion.Euler( 0f, 0f, _cardRotationAmount );
            button.GetComponent<RectTransform>().rotation = newRotation;

            if( button == GetBottomCard() ){
                var setRotation = Quaternion.Euler( 0f, 0f, 0f );
                button.GetComponent<RectTransform>().rotation = setRotation;
                button.GetComponent<RectTransform>().SetAsLastSibling();
            }
        }
    }

    private Button GetBottomCard(){
        Button card;

        for( int i = 0; i < _battleMenuSM.Buttons.Length; i++ ){
            if( _activeButton == _battleMenuSM.Buttons[i] ){
                if( i == 3 ){
                    card = _battleMenuSM.Buttons[0];
                    return card;
                }
                else{
                    card = _battleMenuSM.Buttons[ i + 1 ];
                    return card;
                }
            }
        }

        return default;
    }
}
