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
    [SerializeField] private BattleUIActions _battleUIActions;
    private StateStackMachine<PlayerBattleMenu> _battleMenuSM;

    //-------------------------------------------

    private void OnEnable(){
        BattleUIActions.OnFightMenuOpened += () => OpenMenuButton( _fightButton, _fightOutline );
        BattleUIActions.OnFightMenuClosed += () => CloseMenuButton( _fightButton, _fightOutline );
        BattleUIActions.OnPkmnMenuOpened += () => OpenMenuButton( _pkmnButton, _pkmnOutline );
        BattleUIActions.OnPkmnMenuClosed += () => CloseMenuButton( _pkmnButton, _pkmnOutline );
    }

    private void OnDisable(){
        BattleUIActions.OnFightMenuOpened += () => OpenMenuButton( _fightButton, _fightOutline );
        BattleUIActions.OnFightMenuClosed += () => CloseMenuButton( _fightButton, _fightOutline );
        BattleUIActions.OnPkmnMenuOpened += () => OpenMenuButton( _pkmnButton, _pkmnOutline );
        BattleUIActions.OnPkmnMenuClosed += () => CloseMenuButton( _pkmnButton, _pkmnOutline );
    }

    /// <summary>
    /// so, first order of business will be to convert each menu into its own state to be pushed.
    /// from there, we can simplify the action calls into a single one that gets the specific
    /// menu passed when the event is raised, so that all menus can simply raise a singular action
    /// 
    /// from there, i need to convert the actual opening and closing of menus into pushing and popping them
    /// as states from...somewhere.
    /// currently, when we press a button assigned to a battle menu, it opens that menu as expected. then,
    /// onenable() raises an event saying it was opened. that triggers a few things, such as animating it into place,
    /// animating the outline effect, and also setting all base menu buttons inactive so they can't be selected (which
    /// is something i will no longer need to do, since i will be changing the menu from button assignments to a 
    /// left and right card shuffle style menu).
    /// 
    /// right now, menu buttons are hard-coded to open from an Input Action Property. When that input action event is raised,
    /// it triggers a menu opening function in BattleButtonSelect.cs ( OnDickieDoo() lol )
    /// 
    /// instead, we will be giving the illusion of shuffling cards via animation, while what we're actually doing is just moving
    /// across horizontally selectable buttons. OnSubmit()? i believe is what is called when the default submit UI input action ((A) button)
    /// is pressed. OnSubmit() will then have to statemachine.push() the correct context menu as the active state
    /// </summary>

    private void OpenMenuButton( Button button, GameObject outline ){
        LeanTween.moveLocalX( button.gameObject, 505f, 0.1f );
        _fightSelectButton.SetActive( false ); //--these make it so that the buttons cannot be pressed after a menu has been opened
        _pkmnSelectButton.SetActive( false ); //--these make it so that the buttons cannot be pressed after a menu has been opened
        itemSelectButton.SetActive( false ); //--these make it so that the buttons cannot be pressed after a menu has been opened
        _runSelectButton.SetActive( false ); //--these make it so that the buttons cannot be pressed after a menu has been opened
        outline.SetActive( true );
        LeanTween.moveLocalX( outline, 499, 0.2f );
    }

    private void CloseMenuButton( Button button, GameObject outline ){
        LeanTween.moveLocalX( button.gameObject, 525.1798f, 0.1f );
        LeanTween.moveLocalX( outline, 519, 0.2f );
        outline.SetActive( false );
        _fightSelectButton.SetActive( true );
        _pkmnSelectButton.SetActive( true );
        itemSelectButton.SetActive( true );
        _runSelectButton.SetActive (true );
    }

    private void RestoreMenu(){
        LeanTween.moveLocalX( gameObject, 0f, 0f );
    }

    private void HideMenu(){
        LeanTween.moveLocalX( gameObject, 2000f, 0f );
    }
}
