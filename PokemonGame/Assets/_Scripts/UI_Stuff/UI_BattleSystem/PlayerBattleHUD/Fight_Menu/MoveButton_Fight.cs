using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class MoveButton_Fight : MonoBehaviour, ISelectHandler, IDeselectHandler, ICancelHandler, ISubmitHandler
{
    [SerializeField] private BattleSystem _battleSystem;
    [SerializeField] private FightMenu _fightMenu;
    [SerializeField] private TextMeshProUGUI _moveDescription;
    public Move AssignedMove { get; set; }
    public Button ThisButton { get; private set; }


    private void OnEnable()
    {
        gameObject.GetComponent<Outline>().enabled = false;
        ThisButton = gameObject.GetComponent<Button>();
    }

    public void OnSelect( BaseEventData baseEventData )
    {
        gameObject.GetComponent<Outline>().enabled = true;

        if( AssignedMove != null )
            _moveDescription.text = AssignedMove.MoveSO.Description;
        else
            _moveDescription.text = "-";

        AudioController.Instance.PlaySFX( SoundEffect.ButtonSelect );
    }

    public void OnDeselect( BaseEventData baseEventData )
    {
        gameObject.GetComponent<Outline>().enabled = false;
    }

    public void OnSubmit( BaseEventData baseEventData )
    {
        //--Imprison Check
        if( _battleSystem.IsImprisoned( AssignedMove, _fightMenu.ActiveUnit ) )
        {
            DialogueManager.Instance.PlaySystemMessage( $"{AssignedMove.MoveSO.Name} is imprisoned and can't be used!" );
            return;
        }

        //--Choice Item Check
        if( _fightMenu.ActiveUnit.Flags[UnitFlags.ChoiceItem].IsActive )
        {
            if( _fightMenu.ActiveUnit.LastUsedMove != null && _fightMenu.ActiveUnit.LastUsedMove != AssignedMove )
            {
                DialogueManager.Instance.PlaySystemMessage( $"{_fightMenu.ActiveUnit.Pokemon.HeldItem.ItemName} prevents use of any move that isn't {_fightMenu.ActiveUnit.LastUsedMove.MoveSO.Name}!" );
                return;
            }
        }

        //--If the move has enough PP to use
        if( AssignedMove.PP > 0){
            //--Pop State to prevent extra inputs
            _fightMenu.BattleMenu.StateMachine.Pop();

            //--If the battle is a double battle, we handle target selection.
            //--If not, we simply push the command to the queue, as there is only one target.
            //--May need to alter this to include other multi-target fights.
            if( _battleSystem.BattleType == BattleType.TrainerDoubles )
                _fightMenu.BattleMenu.HandleMoveTargetSelection( _fightMenu.ActiveUnit, AssignedMove );
            else
                _battleSystem.SetMoveCommand( _fightMenu.ActiveUnit, _battleSystem.EnemyUnits, AssignedMove );

            //--Assign this as the last-used move for the Active BattleUnit.
            _fightMenu.ActiveUnit.SetLastUsedMove( AssignedMove );
        }
        else{
            DialogueManager.Instance.PlaySystemMessage( "There's no PP left!" );
            return;
        }
    }

    public void OnCancel( BaseEventData baseEventData ){
        _fightMenu.ClearMemoryButton();
        BattleUIActions.OnSubMenuClosed?.Invoke();
        BattleUIActions.OnFightMenuClosed?.Invoke();
        StartCoroutine( WaitForCloseAnims() );
    }

    private IEnumerator WaitForCloseAnims(){
        yield return new WaitForSeconds( 0.1f );
        gameObject.GetComponent<Outline>().enabled = false;
        _fightMenu.BattleMenu.StateMachine.Pop();
    }
}
