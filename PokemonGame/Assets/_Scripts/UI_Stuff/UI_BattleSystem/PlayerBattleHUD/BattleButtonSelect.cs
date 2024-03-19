using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections;

public class BattleButtonSelect : MonoBehaviour, ISelectHandler, IDeselectHandler, ISubmitHandler, ICancelHandler
{
    [SerializeField] private PlayerBattleMenu _battleMenu;
    [SerializeField] private GameObject _menuOpen;
    private Button _thisButton;
    private BattleUIActions _battleUIActions;
    public int ButtonCurrentPosition { get; private set; }

    private void OnEnable(){
        _thisButton = GetComponent<Button>();

        Debug.Log( _thisButton + " " + gameObject );
    }

    private void Start(){
        _battleUIActions = _battleMenu.BUIActions;
    }

    public void OnSelect( BaseEventData eventData ){
        _battleUIActions.OnButtonSelected?.Invoke( _thisButton );
        Debug.Log( "selected: " + gameObject );
    }

    public void OnDeselect( BaseEventData eventData ){
        _battleUIActions.OnButtonDeselected?.Invoke( _thisButton );
        Debug.Log( "deselected: " + gameObject );
    }

    public void OnSubmit( BaseEventData eventData ){
        Debug.Log( "submitted you titted !!!!!!!!" );
        StartCoroutine( WaitForMenuOpen() );
    }

    public void OnCancel( BaseEventData eventData ){
        //--this is here so we hopefully do nothing on cancel, at least for now. perhaps i can implement a hold to run feature
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
