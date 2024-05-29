using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LearnMoveButton_Pause : MonoBehaviour, ISelectHandler, IDeselectHandler, ICancelHandler, ISubmitHandler
{
    private LearnMove_Pause _learnMenu;
    private MoveSO _assignedMove;
    public Button ThisButton { get; private set; }

    private void OnEnable(){
        ThisButton = GetComponent<Button>();
    }

    public void Setup( ILearnMoveContext learnMenu, MoveSO move ){
        _learnMenu = (LearnMove_Pause)learnMenu;
        _assignedMove = move;
    }

    public void OnSelect( BaseEventData eventData ){
        Debug.Log( "selected: " + gameObject.GetComponent<Button>().name );
    }

    public void OnDeselect( BaseEventData eventData ){
        
    }

    public void OnSubmit( BaseEventData eventData ){
        Debug.Log( $"submitted: {_assignedMove.Name}" );
        if( _assignedMove == _learnMenu.NewMove )
            _learnMenu.DontReplaceMove();
        else
            _learnMenu.ReplaceMove( _assignedMove );
    }

    public void OnCancel( BaseEventData eventData ){
        _learnMenu.DontReplaceMove();
    }

}
