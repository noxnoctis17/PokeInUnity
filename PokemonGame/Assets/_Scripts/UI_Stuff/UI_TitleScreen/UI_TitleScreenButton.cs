using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UI_TitleScreenButton : MonoBehaviour, ISelectHandler, IDeselectHandler, ISubmitHandler, ICancelHandler
{
    private Button _thisButton;
    private UI_TitleScreen _titleScreen;

    public void Setup( UI_TitleScreen ts )
    {
        _titleScreen = ts;
    }

    public void OnSelect( BaseEventData eventData )
    {
        AudioController.Instance.PlaySFX( SoundEffect.ButtonSelect );
    }

    public void OnDeselect( BaseEventData eventData )
    {

    }

    public void OnSubmit( BaseEventData eventData )
    {
        AudioController.Instance.PlaySFX( SoundEffect.ButtonSelect );
        
    }

    public void OnCancel( BaseEventData eventData )
    {
        AudioController.Instance.PlaySFX( SoundEffect.ButtonSelect );
    }
}
