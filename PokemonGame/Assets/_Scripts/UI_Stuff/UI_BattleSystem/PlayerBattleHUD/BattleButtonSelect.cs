using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
        _battleMenu.OnPushNewState?.Invoke( _state );
    }
    
}
