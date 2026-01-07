using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class ItemButton_Battle : MonoBehaviour, ISelectHandler, IDeselectHandler, ICancelHandler, ISubmitHandler
{
    [SerializeField] private BattleSystem _battleSystem;
    [SerializeField] private BagScreen_Battle _bagMenu;


    public void OnSelect( BaseEventData eventData ){
        AudioController.Instance.PlaySFX( SoundEffect.ButtonSelect );
    }

    public void OnDeselect( BaseEventData eventData ){
        
    }

    public void OnCancel( BaseEventData eventData ){
        BattleUIActions.OnSubMenuClosed?.Invoke();
        StartCoroutine( WaitForCloseAnims() );
    }

    public void OnSubmit( BaseEventData eventData ){
        _bagMenu.BattleMenu.StateMachine.Pop();
        
        if( _battleSystem.BattleType == BattleType.TrainerSingles || _battleSystem.BattleType == BattleType.TrainerDoubles ){
            // StartCoroutine ( _battleSystem.AbilityCutIn.CutIn( $"You can't steal another trainer's Pokemon!" ) );
            DialogueManager.Instance.PlaySystemMessage( "You can't steal another trainer's Pokemon!" );
            return;
        }
        
        BattleUIActions.OnSubMenuClosed?.Invoke();
        // _battleSystem.SetUseItemCommand( /*eventually will be the item or something*/ );
        BattleUIActions.OnCommandUsed?.Invoke();
    }

    private IEnumerator WaitForCloseAnims(){
        yield return new WaitForSeconds( 0.1f );
        _bagMenu.BattleMenu.StateMachine.Pop();
    }
}
