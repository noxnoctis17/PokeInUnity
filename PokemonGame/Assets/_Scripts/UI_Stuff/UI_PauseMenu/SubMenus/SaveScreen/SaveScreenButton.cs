using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum SaveScreenOption { Save, Load }
public class SaveScreenButton : MonoBehaviour, ISubmitHandler, ICancelHandler
{
    private UI_PauseMenuStateMachine _pauseMenuStateMachine;
    private UI_SaveScreen _saveScreen;
    private Button _saveButton;
    [SerializeField] SaveScreenOption _saveOption;
    
    public void Setup( UI_PauseMenuStateMachine stateMachine, UI_SaveScreen saveScreen ){
        _pauseMenuStateMachine = stateMachine;
        _saveScreen = saveScreen;
        _saveButton = GetComponent<Button>();

        _saveButton.Select();
    }

    public void OnSubmit( BaseEventData eventData ){
        if( _saveOption == SaveScreenOption.Save )
        {
            SavingSystem.Instance.Save( "SaveSlot_1" );
            _saveScreen.OnSaveMade?.Invoke();
        }
        else if( _saveOption == SaveScreenOption.Load )
        {
            SavingSystem.Instance.Load( "SaveSlot_1" );
        }
    }
    
    public void OnCancel( BaseEventData eventData ){
        _pauseMenuStateMachine.PopState();
    }
}
