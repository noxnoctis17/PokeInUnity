using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PlayerBattleMenu : MonoBehaviour
{
    [SerializeField] private Button _fightButton, _pkmnButton, _itemButton, _runButton;
    [SerializeField] private GameObject _fightOutline, _pkmnOutline, _itemOutline, _runOutline;
    [SerializeField] private GameObject _fightSelectButton, _pkmnSelectButton, itemSelectButton, _runSelectButton;
    [SerializeField] private BattleSystem _battleSystem;
    [SerializeField] private EventSystem _eventSystem;
    public EventSystem eventSystem => _eventSystem;

    //-------------------------------------------

    private void OnEnable(){
        BattleUIActions.OnFightMenuOpened += () => OpenMenuButton(_fightButton, _fightOutline);
        BattleUIActions.OnFightMenuClosed += () => CloseMenuButton(_fightButton, _fightOutline);
        BattleUIActions.OnPkmnMenuOpened += () => OpenMenuButton(_pkmnButton, _pkmnOutline);
        BattleUIActions.OnPkmnMenuClosed += () => CloseMenuButton(_pkmnButton, _pkmnOutline);
    }

    private void OnDisable(){
        BattleUIActions.OnFightMenuOpened += () => OpenMenuButton(_fightButton, _fightOutline);
        BattleUIActions.OnFightMenuClosed += () => CloseMenuButton(_fightButton, _fightOutline);
        BattleUIActions.OnPkmnMenuOpened += () => OpenMenuButton(_pkmnButton, _pkmnOutline);
        BattleUIActions.OnPkmnMenuClosed += () => CloseMenuButton(_pkmnButton, _pkmnOutline);
    }

    private void OpenMenuButton(Button button, GameObject outline){
        LeanTween.moveLocalX(button.gameObject, 505f, 0.1f);
        _fightSelectButton.SetActive(false);
        _pkmnSelectButton.SetActive(false);
        itemSelectButton.SetActive(false);
        _runSelectButton.SetActive(false);
        outline.SetActive(true);
        LeanTween.moveLocalX(outline, 499, 0.2f);
    }

    private void CloseMenuButton(Button button, GameObject outline){
        LeanTween.moveLocalX(button.gameObject, 525.1798f, 0.1f);
        LeanTween.moveLocalX(outline, 519, 0.2f);
        outline.SetActive(false);
        _fightSelectButton.SetActive(true);
        _pkmnSelectButton.SetActive(true);
        itemSelectButton.SetActive(true);
        _runSelectButton.SetActive(true);
    }

    private void RestoreMenu(){
        LeanTween.moveLocalX(gameObject, 0f, 0f);
    }

    private void HideMenu(){
        LeanTween.moveLocalX(gameObject, 2000f, 0f);
    }
}
