using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RunButton : MonoBehaviour, ISubmitHandler, ICancelHandler
{
    private BattleSystem _battleSystem;
    private PlayerBattleMenu _battleMenu;

    public void Setup( BattleSystem battleSystem, PlayerBattleMenu battleMenu ){
        _battleSystem = battleSystem;
        _battleMenu = battleMenu;
    }

    public void OnSubmit( BaseEventData eventData ){
        _battleMenu.BattleMenuStateMachine.Pop();
        _battleSystem.SetRunFromBattleCommand();
        BattleUIActions.OnCommandUsed?.Invoke();
        BattleUIActions.OnSubMenuClosed?.Invoke();
    }

    public void OnCancel( BaseEventData baseEventData ){
        BattleUIActions.OnSubMenuClosed?.Invoke();
        StartCoroutine( WaitForCloseAnims() );
    }

    private IEnumerator WaitForCloseAnims(){
        yield return new WaitForSeconds( 0.1f );
        _battleMenu.BattleMenuStateMachine.Pop();
    }
}
