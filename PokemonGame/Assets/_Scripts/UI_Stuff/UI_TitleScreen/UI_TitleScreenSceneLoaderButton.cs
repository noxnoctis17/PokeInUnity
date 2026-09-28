using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UI_TitleScreenSceneLoaderButton : MonoBehaviour, ISelectHandler, IDeselectHandler, ISubmitHandler, ICancelHandler
{
    private Button _thisButton;
    private UI_TitleScreen _titleScreen;
    [SerializeField] private SceneDetails _scene;
    [SerializeField] private bool _isFreeRoam;

    public void Setup( UI_TitleScreen ts )
    {
        _thisButton = gameObject.GetComponent<Button>();
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

        if( _isFreeRoam )
            StartCoroutine( _titleScreen.LoadOverworld() );
        else
            _titleScreen.LoadScene( _scene );
    }

    public void OnCancel( BaseEventData eventData )
    {
        AudioController.Instance.PlaySFX( SoundEffect.ButtonSelect );
    }
}
