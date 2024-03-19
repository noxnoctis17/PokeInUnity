using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerBattleMenu : MonoBehaviour
{
    //================================================================================
    [SerializeField] private Button _fightButton, _pkmnButton, _bagButton, _runButton;
    [SerializeField] private BattleSystem _battleSystem;
    public BattleUIActions BUIActions { get; private set; }
    private StateStackMachine<PlayerBattleMenu> _battleMenuSM;
    private Button[] _buttons;
    private Button _activeButton;
    private Button _memorizeButton;
    private Button _previousButton;
    private PlayerInput _playerInput;
    private float _cardRotationAmount;

    //================================================================================

    private void OnEnable(){
        //--Reference Assignments
        _playerInput = PlayerReferences.Instance.PlayerInput;
        BUIActions = GetComponent<BattleUIActions>();

        //--Set Button Array
        _buttons = new Button[] { _runButton, _bagButton, _pkmnButton, _fightButton  };

        //--Events
        _playerInput.UI.Navigate.performed += OnNavigate;
        BUIActions.OnButtonSelected += SetActiveButton;

        //--Select Initial Button
        _cardRotationAmount = 15f;
        StartCoroutine( SelectInitialButton() );
    
    }

    private void OnDisable(){
        //--Events
        _playerInput.UI.Navigate.performed -= OnNavigate;

        //--Controls
        PlayerReferences.Instance.DisableUI();
        PlayerReferences.Instance.EnableCharacterControls();
    
    }

    //--may need to add a small delay before selecting initial button due to code order and stuff happening fast
    //--it currently isn't able to select a button right away because of...reasons, apparently. this could be the place to animate
    //--the menu into place, giving everything some time to load up before actions are called. i'll give it a try tonight 3/17/24
    //--the delay worked
    private IEnumerator SelectInitialButton(){
        yield return new WaitForSeconds( 1f );

        //--Controls Swap after Delay
        PlayerReferences.Instance.DisableCharacterControls();
        PlayerReferences.Instance.EnableUI();

        //--Select Initial Button
        if( _memorizeButton == null )
            _activeButton = _fightButton;
        else
            _activeButton = _memorizeButton;

        if( _previousButton == null )
            _previousButton = _runButton;

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
        }

        //--Player moved through the menu to the left
        if( direction.x < 0 ){
            LeftcreasePositions();
        }
    }

    //--Increase position value
    private void LeftcreasePositions(){
        foreach( Button button in _buttons ){
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
        foreach( Button button in _buttons ){
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

        for( int i = 0; i < _buttons.Length; i++ ){
            if( _activeButton == _buttons[i] ){
                if( i == 3 ){
                    card = _buttons[0];
                    return card;
                }
                else{
                    card = _buttons[ i + 1 ];
                    return card;
                }
            }
        }

        return default;
    }

    // private void OpenMenuButton( Button button/*, GameObject outline*/ ){
    //     LeanTween.moveLocalX( button.gameObject, 505f, 0.1f );
    //     // _fightSelectButton.SetActive( false );  //--these make it so that the buttons cannot be pressed after a menu has been opened
    //     // _pkmnSelectButton.SetActive( false );   //--these make it so that the buttons cannot be pressed after a menu has been opened
    //     // _itemSelectButton.SetActive( false );    //--these make it so that the buttons cannot be pressed after a menu has been opened
    //     // _runSelectButton.SetActive( false );    //--these make it so that the buttons cannot be pressed after a menu has been opened
    //     // outline.SetActive( true );
    //     // LeanTween.moveLocalX( outline, 499, 0.2f );
    // }

    // private void CloseMenuButton( Button button/*, GameObject outline*/ ){
    //     LeanTween.moveLocalX( button.gameObject, 525.1798f, 0.1f );
    //     // LeanTween.moveLocalX( outline, 519, 0.2f );
    //     // outline.SetActive( false );
    //     // _fightSelectButton.SetActive( true );
    //     // _pkmnSelectButton.SetActive( true );
    //     // _itemSelectButton.SetActive( true );
    //     // _runSelectButton.SetActive (true );
    // }

    // private void RestoreMenu(){
    //     LeanTween.moveLocalX( gameObject, 0f, 0f );
    // }

    // private void HideMenu(){
    //     LeanTween.moveLocalX( gameObject, 2000f, 0f );
    // }
}
