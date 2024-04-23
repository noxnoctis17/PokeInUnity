using NoxNoctisDev.StateMachine;
using UnityEngine;

public class BattleMenu_PausedState : State<PlayerBattleMenu>
{
    private PlayerBattleMenu _battleMenu;

    public override void EnterState( PlayerBattleMenu owner ){
        Debug.Log( "Entering BattleMenu_PausedState" );
        _battleMenu = owner;

        //--Disable Battle Controls
        PlayerReferences.Instance.PlayerController.DisableBattleControls();
        // PlayerReferences.Instance.PlayerController.DisableUI();

        //--Clear Selected Button so that the BattleHUD UI is no longer navigable or interactable until we return to the base battlehud state
        ClearSelectedButton();
    }

    public override void ExitState(){
        Debug.Log( "Exiting BattleMenu_PausedState" );
        PlayerReferences.Instance.PlayerController.EnableBattleControls();
        // PlayerReferences.Instance.PlayerController.EnableUI();
    }

    private void ClearSelectedButton(){
        _battleMenu.BattleSystem.EventSystem.SetSelectedGameObject( null );

        //--Disable Menu Buttons
        _battleMenu.DisableMenuButtons();
    }
}
