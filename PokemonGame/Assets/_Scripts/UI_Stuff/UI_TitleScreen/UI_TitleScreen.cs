using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class UI_TitleScreen : MonoBehaviour
{
    public SceneHandler SceneHandler { get; private set; }

    [SerializeField] private Button _freeRoamButton;
    [SerializeField] private Button _stadiumButton;
    [SerializeField] private Button _optionsButton;
    [SerializeField] private Button _quitButton;
    [SerializeField] private Image _fade;

    private Button[] _buttons;
    private Button _initialButton;
    public Button LastButton { get; private set; }

    private void Start()
    {
        SceneHandler = SceneHandler.Instance;
        GameStateController.Instance.PushGameState( TitleScreenState.Instance );

        _buttons = new Button[]{ _freeRoamButton, _stadiumButton, _optionsButton, _quitButton };
        _initialButton = _freeRoamButton;
        StartCoroutine( SetInitialButton() );
    }

    public void LoadScene( SceneDetails scene )
    {
        StartCoroutine( LoadGameMode( scene ) );
    }

    public IEnumerator LoadOverworld()
    {
        yield return null;
        yield return _fade.DOFade( 255, 0.5f );

        yield return SceneHandler.LoadOverworld();
        yield return null;

        gameObject.SetActive( false );
        yield return _fade.DOFade( 0, 1f );
        yield return null;
        GameStateController.Instance.PushGameState( FreeRoamState.Instance );
    }

    private IEnumerator LoadGameMode( SceneDetails scene )
    {
        yield return null;
        yield return _fade.DOFade( 255, 0.5f );

        scene.LoadSceneAdditively();
        yield return null;

        gameObject.SetActive( false );
        yield return _fade.DOFade( 0, 1f );
        yield return null;
        // GameStateController.Instance.PushGameState( StadiumModeState.Instance );
    }

    private IEnumerator SetInitialButton()
    {
        yield return new WaitForSeconds( 0.15f );
        if( LastButton != null )
            SelectMemoryButton();
        else{
            SetMemoryButton( _initialButton );
        }
    }

    public void SetMemoryButton( Button lastButton )
    {
        LastButton = lastButton;
        SelectMemoryButton();
    }

    private void SelectMemoryButton()
    {
        LastButton.Select();
    }

    public void ClearMemoryButton()
    {
        LastButton = null;
        _initialButton.Select();
    }
}
