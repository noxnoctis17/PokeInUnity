using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SaveScreenButton : MonoBehaviour, ISubmitHandler, ICancelHandler
{
    private UI_PauseMenuStateMachine _pauseMenuStateMachine;
    private UI_SaveScreen _saveScreen;
    private Button _saveButton;
    
    public void Setup( UI_PauseMenuStateMachine stateMachine, UI_SaveScreen saveScreen ){
        _pauseMenuStateMachine = stateMachine;
        _saveScreen = saveScreen;
        _saveButton = GetComponent<Button>();

        _saveButton.Select();
    }

    public void OnSubmit( BaseEventData eventData ){
        SavingSystem.Instance.Save( "SaveSlot_1" );
        _saveScreen.OnSaveMade?.Invoke();
    }
    
    public void OnCancel( BaseEventData eventData ){
        _pauseMenuStateMachine.PopState();
    }
}
