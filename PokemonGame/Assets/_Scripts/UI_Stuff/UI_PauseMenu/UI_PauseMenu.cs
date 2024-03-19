using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UI_PauseMenu : MonoBehaviour
{
    [SerializeField] private Button _fight, _party, _bag, _run;
    private Button[] _buttons;
    public UI_PauseMenuButton_Events ButtonEvents { get; private set; }
    private Button _activeButton;
    private Button _memorizeButton;
    private Button _previousButton;
    private PlayerInput _playerInput;
    private float _cardRotationAmount;

    /// <summary>
    /// to handle the card shuffling on the menu, each time you select a button to the right
    /// the currently selected button will always get sent to the back most position.
    /// the cards, which never actually move in the X or Y direction, and instead only rotate from the bottom left
    /// corner pivot by 15 degrees relative to the previous card ( 0, -15, -30, -45 ) will increase
    /// their rotation by 15 degrees. if the way i've imagined this and sorta tested IRL works out correctly, what will happen is this:
    /// the layering of all cards will stay consistent, with the top most being sent to the back, the one under it will now be the top most,
    /// and so on down the line. they will all rotate by 15 degrees in the correct direction.
    /// 
    /// if you scroll to the left, the opposite should happen. the bottom most card should be brought to the top, and its rotation should be
    /// changed from -45 to 0, while all cards will have their rotation "decreased" by 15 degrees
    /// </summary>

    private void OnEnable(){
        //--Components
        _playerInput = PlayerReferences.Instance.PlayerInput;
        ButtonEvents = GetComponent<UI_PauseMenuButton_Events>();

        //--Set Button Array
        _buttons = new Button[] { _fight, _party, _bag, _run };

        Debug.Log( _playerInput );
        //--Reference Assignments
        PlayerReferences.Instance.DisableCharacterControls();
        PlayerReferences.Instance.EnableUI();

        //--Events
        _playerInput.UI.Navigate.performed += OnNavigate;
        _playerInput.UI.Cancel.performed += CloseMenu;
        ButtonEvents.OnButtonSelected += SetActiveButton;

        //--Other
        _cardRotationAmount = 15;
        SelectInitialButton();
    }

    private void OnDisable(){
        //--Events
        _playerInput.UI.Navigate.performed -= OnNavigate;
        ButtonEvents.OnButtonSelected -= SetActiveButton;

        PlayerReferences.Instance.DisableUI();
        PlayerReferences.Instance.EnableCharacterControls();
    }

    private void SelectInitialButton(){
        Debug.Log( _memorizeButton );
        if( _memorizeButton == null )
            _activeButton = _fight;
        else
            _activeButton = _memorizeButton;

        if( _previousButton == null )
            _previousButton = _run;

        _activeButton.Select();
    }

    private void SetActiveButton( Button button ){
        _previousButton = _activeButton;
        _activeButton = button;
        _memorizeButton = _activeButton;
    }

    private void OnNavigate( InputAction.CallbackContext context ){
        Vector2 direction = context.ReadValue<Vector2>();

        //--Player moved through the menu to the right
        if( direction.x > 0 ){
            RightcreasePositions();
            Debug.Log( "rightcrease" );
        }

        //--Player moved through the menu to the left
        if( direction.x < 0 ){
            LeftcreasePositions();
            Debug.Log( "leftcrease" );
        }
    }

    private void CloseMenu( InputAction.CallbackContext context ){
        gameObject.SetActive( false );
    }

    //--Increase position value
    private void RightcreasePositions(){
        foreach( Button button in _buttons ){
            var newRotation = button.GetComponent<RectTransform>().rotation * Quaternion.Euler( 0f, 0f, _cardRotationAmount );
            button.GetComponent<RectTransform>().rotation = newRotation;

            if( button == _activeButton ){
                var setRotation = Quaternion.Euler( 0f, 0f, -45f );
                button.GetComponent<RectTransform>().rotation = setRotation;
            }
        }

        _activeButton.GetComponent<RectTransform>().SetAsFirstSibling();
    }

    //--Decrease position value
    private void LeftcreasePositions(){
        foreach( Button button in _buttons ){
            var newRotation = button.GetComponent<RectTransform>().rotation * Quaternion.Euler( 0f, 0f, -_cardRotationAmount );
            button.GetComponent<RectTransform>().rotation = newRotation;

            if( button == GetBottomCard() ){
                var setRotation = Quaternion.Euler( 0f, 0f, 0f );
                button.GetComponent<RectTransform>().rotation = setRotation;
            }
        }

        GetBottomCard().GetComponent<RectTransform>().SetAsLastSibling();
    }

    private Button GetBottomCard(){
        Button card;

        for( int i = 0; i < _buttons.Length; i++ ){
            if( _activeButton == _buttons[i] ){
                if( i == 0 ){
                    card = _buttons[3];
                    return card;
                }
                else{
                    card = _buttons[ i - 1 ];
                    return card;
                }
            }
        }

        return default;
    }
}
