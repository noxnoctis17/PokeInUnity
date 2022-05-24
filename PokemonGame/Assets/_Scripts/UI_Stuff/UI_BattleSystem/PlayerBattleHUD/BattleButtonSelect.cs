using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

public class BattleButtonSelect : MonoBehaviour
{
    [SerializeField] private PlayerBattleMenu _battleMenu;
    [SerializeField] private GameObject _menuOpen;
    [SerializeField] private InputActionProperty _submenuSubmit;
    private Button _thisButton;
    
    
    private void Awake(){
        _thisButton = this.GetComponent<Button>();
    }

    private void OnEnable(){
        BattleUIActions.OnSubMenuOpened += DisableInput;
        BattleUIActions.OnSubMenuClosed += EnableInput;
        BattleUIActions.OnBattleSystemBusy += DisableInput;
        BattleSystem.OnPlayerAction += EnableInput;
        _submenuSubmit.action.performed += OnDickiedoo;
        EnableInput();
    }

    private void OnDisable(){
        BattleUIActions.OnSubMenuOpened -= DisableInput;
        BattleUIActions.OnSubMenuClosed -= EnableInput;
        BattleUIActions.OnBattleSystemBusy -= DisableInput;
        BattleSystem.OnPlayerAction -= EnableInput;
        _submenuSubmit.action.performed -= OnDickiedoo;
        DisableInput();
    }

    private void EnableInput(){
        _submenuSubmit.action.Enable();
    }

    private void DisableInput(){
        _submenuSubmit.action.Disable();
    }

    private void OnDickiedoo(InputAction.CallbackContext context){
        BattleUIActions.OnSubMenuOpened?.Invoke();
        StartCoroutine(WaitForMenuOpen());
    }

    private IEnumerator WaitForMenuOpen(){
        yield return new WaitForSeconds(0.1f);
        _menuOpen.SetActive(true);
    }

}
