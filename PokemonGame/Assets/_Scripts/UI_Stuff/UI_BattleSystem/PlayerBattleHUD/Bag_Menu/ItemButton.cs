using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class ItemButton : MonoBehaviour, ISelectHandler, IDeselectHandler, ICancelHandler, ISubmitHandler
{
    [SerializeField] private BattleSystem _battleSystem;
    [SerializeField] private BagMenu _bagMenu;


    public void OnSelect( BaseEventData eventData ){
        
    }

    public void OnDeselect( BaseEventData eventData ){
        
    }

    public void OnCancel( BaseEventData eventData ){
        BattleUIActions.OnSubMenuClosed?.Invoke();
        StartCoroutine( WaitForCloseAnims() );
    }

    public void OnSubmit( BaseEventData eventData ){
        BattleUIActions.OnSubMenuClosed?.Invoke();
        _battleSystem.SetUseItemCommand( /*eventually will be the item or something*/ );
        BattleUIActions.OnCommandUsed?.Invoke();
        _bagMenu.BattleMenu.BattleMenuStateMachine.Pop();
    }

    private IEnumerator WaitForCloseAnims(){
        yield return new WaitForSeconds( 0.1f );
        _bagMenu.BattleMenu.BattleMenuStateMachine.Pop();
    }
}
