using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using NoxNoctisDev.StateMachine;

public class BattleButtonSelect : MonoBehaviour, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    [SerializeField] private PlayerBattleMenu _battleMenu;
    [SerializeField] private GameObject _menuOpen;
    [SerializeField] private State<PlayerBattleMenu> _state;
    private Button _thisButton;
    private BattleUIActions _battleUIActions;

    private void OnEnable(){
        _thisButton = GetComponent<Button>();
    }

    private void Start(){
        _battleUIActions = _battleMenu.BUIActions;
    }

    public void OnSelect( BaseEventData eventData ){
        _battleUIActions.OnButtonSelected?.Invoke( _thisButton );
    }

    public void OnDeselect( BaseEventData eventData ){
        _battleUIActions.OnButtonDeselected?.Invoke( _thisButton );
    }

    public void OnSubmit( BaseEventData eventData ){
        Debug.Log( "OnSubmit: " + _battleMenu );
        Debug.Log( "OnSubmit: " + _state );
        _battleMenu.OnChangeState?.Invoke( _state );
    }
    
//==============[] OLD ]=============================
    // private void Awake(){
    //     _thisButton = GetComponent<Button>();
    // }

    // private void OnEnable(){
    //     BattleUIActions.OnSubMenuOpened += DisableInput;
    //     BattleUIActions.OnSubMenuClosed += EnableInput;
    //     BattleUIActions.OnBattleSystemBusy += DisableInput;
    //     BattleSystem.OnPlayerAction += EnableInput;
    //     _submenuSubmit.action.performed += OnDickiedoo;
    //     EnableInput();
    // }

    // private void OnDisable(){
    //     BattleUIActions.OnSubMenuOpened -= DisableInput;
    //     BattleUIActions.OnSubMenuClosed -= EnableInput;
    //     BattleUIActions.OnBattleSystemBusy -= DisableInput;
    //     BattleSystem.OnPlayerAction -= EnableInput;
    //     _submenuSubmit.action.performed -= OnDickiedoo;
    //     DisableInput();
    // }

    // private void EnableInput(){
    //     _submenuSubmit.action.Enable();
    // }

    // private void DisableInput(){
    //     _submenuSubmit.action.Disable();
    // }

    // private void OnDickiedoo( InputAction.CallbackContext context ){
    //     BattleUIActions.OnSubMenuOpened?.Invoke();
    //     StartCoroutine( WaitForMenuOpen() );
    // }

    //--do i really need to run a coroutine and also wait this long? possibly.
    //--menu stuff + its animations might be a fantastic candidate to swap to async
    //--i can convert dotweet animations and such into tasks, and wait X amount of time simply
    //--by using await task, rather than having to harcode in odd wait points like this and so on
    private IEnumerator WaitForMenuOpen(){
        yield return new WaitForSeconds( 0.1f );
        _menuOpen.SetActive( true );
    }
}
