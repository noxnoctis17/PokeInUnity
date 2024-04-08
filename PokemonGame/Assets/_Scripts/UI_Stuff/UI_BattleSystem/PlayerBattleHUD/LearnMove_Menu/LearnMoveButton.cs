using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LearnMoveButton : MonoBehaviour, ISelectHandler, IDeselectHandler, ICancelHandler, ISubmitHandler
{
    private BattleSystem _battleSystem;
    private LearnMoveMenu _learnMenu;
    private MoveBaseSO _assignedMove;
    public Button ThisButton { get; private set; }

    private void OnEnable(){
        ThisButton = GetComponent<Button>();
    }

    public void Setup( BattleSystem battleSystem, LearnMoveMenu learnMenu, MoveBaseSO move ){
        _battleSystem = battleSystem;
        _learnMenu = learnMenu;

        _assignedMove = move;
    }

    public void OnSelect( BaseEventData eventData ){
        Debug.Log( "selected: " + gameObject.GetComponent<Button>().name );
    }

    public void OnDeselect( BaseEventData eventData ){
        
    }

    public void OnSubmit( BaseEventData eventData ){
        Debug.Log( $"submitted: {_assignedMove.MoveName}" );
        _learnMenu.OnReplaceMove?.Invoke( _assignedMove );
    }

    public void OnCancel( BaseEventData eventData ){
        _learnMenu.OnDontReplaceMove?.Invoke();
        // StartCoroutine( WaitForCloseAnims() );
    }

    private IEnumerator WaitForCloseAnims(){
        yield return new WaitForSeconds( 0.1f );
        _learnMenu.BattleMenu.BattleMenuStateMachine.Pop();
    }
}
