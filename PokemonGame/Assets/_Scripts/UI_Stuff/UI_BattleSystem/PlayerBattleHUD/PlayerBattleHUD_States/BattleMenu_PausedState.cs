using NoxNoctisDev.StateMachine;
using UnityEngine.EventSystems;

public class BattleMenu_PausedState : State<PlayerBattleMenu>
{
    private PlayerBattleMenu _battleMenu;

    public override void EnterState( PlayerBattleMenu owner ){
        _battleMenu = owner;

        //--Disable Battle Controls
        PlayerReferences.Instance.DisableBattleControls();

        //--Clear Selected Button so that the BattleHUD UI is no longer navigable or interactable until we return to the base battlehud state
        ClearSelectedButton();
    }

    public override void ExitState(){
        PlayerReferences.Instance.EnableBattleControls();
    }

    private void ClearSelectedButton(){
        EventSystem.current.SetSelectedGameObject( null );

        //--Disable Menu Buttons
        _battleMenu.DisableMenuButtons();
    }
}