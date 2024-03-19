using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UI_PauseMenuButton : MonoBehaviour, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    [SerializeField] private UI_PauseMenu _pauseMenu;
    private Button _thisButton;
    private UI_PauseMenuButton_Events _buttonEvents;
    public int ButtonCurrentPosition { get; private set; }

    private void OnEnable(){
        _buttonEvents = _pauseMenu.ButtonEvents;
        _thisButton = GetComponent<Button>();

        Debug.Log( _thisButton + " " + gameObject );
    }

    public void OnSelect( BaseEventData eventData ){
        _buttonEvents.OnButtonSelected?.Invoke( _thisButton );
        Debug.Log( "selected: " + gameObject );
    }

    public void OnDeselect( BaseEventData eventData ){
        _buttonEvents.OnButtonDeselected?.Invoke( _thisButton );
        Debug.Log( "deselected: " + gameObject );
    }

    public void OnSubmit( BaseEventData eventData ){
        Debug.Log( "submitted you titted !!!!!!!!" );
    }

    public void ChangePosition( int position ){
        
    }
}
