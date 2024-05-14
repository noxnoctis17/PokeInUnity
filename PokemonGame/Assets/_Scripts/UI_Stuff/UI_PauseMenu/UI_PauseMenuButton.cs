using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using NoxNoctisDev.StateMachine;

public class UI_PauseMenuButton : MonoBehaviour, ISelectHandler, IDeselectHandler, ISubmitHandler, ICancelHandler
{
    private UI_PauseMenu _pauseMenu;
    [SerializeField] private State<UI_PauseMenuStateMachine> _pauseMenuState;
    private Button _thisButton;
    private UI_PauseMenuButton_Events _buttonEvents;

    public void Setup( UI_PauseMenu pauseMenu ){
        _pauseMenu = pauseMenu;
        _buttonEvents = _pauseMenu.ButtonEvents;
        _thisButton = GetComponent<Button>();
    }

    public void OnSelect( BaseEventData eventData ){
        _buttonEvents.OnButtonSelected?.Invoke( _thisButton );
        // Debug.Log( "selected: " + gameObject );
    }

    public void OnDeselect( BaseEventData eventData ){
        _buttonEvents.OnButtonDeselected?.Invoke( _thisButton );
        // Debug.Log( "deselected: " + gameObject );
    }

    public void OnSubmit( BaseEventData eventData ){
        // Debug.Log( $"{gameObject.name} was submitted" );
        _buttonEvents.OnButtonSubmitted?.Invoke( _thisButton );
        _pauseMenu.StateMachine.PushState( _pauseMenuState );
    }

    public void OnCancel( BaseEventData eventData ){
        _pauseMenu.StateMachine.PopState();
    }

}
