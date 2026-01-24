using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TargetSelect_Multiple : MonoBehaviour, ISelectHandler, IDeselectHandler, ISubmitHandler, ICancelHandler
{
    [SerializeField] private BattleMenu_TargetSelectState _targetSelect;
    [SerializeField] private Color _fakeHighlight;
    [SerializeField] private Color _originalColor;
    private BattleSystem _battleSystem;
    private Move _moveUsed;
    private List<TargetSelect_Button> _highlightButtons;
    private List<TargetSelect_Button> _targets;
    public Button ThisButton { get; private set; }

    public void Init( Move move, List<TargetSelect_Button> highlightButtons, List<TargetSelect_Button> disableButtons, TargetSelect_Button user, List<TargetSelect_Button> targets )
    {
        ThisButton = GetComponent<Button>();
        _battleSystem = _targetSelect.PlayerBattleMenu.BattleSystem;
        _highlightButtons = highlightButtons;
        _targets = targets;
        _moveUsed = move;

        gameObject.SetActive( true );
        
        if( disableButtons == null )
            disableButtons = new();

        foreach( var button in highlightButtons )
        {
            var colors = button.ThisButton.colors;
            colors.disabledColor = _fakeHighlight;

            button.ThisButton.colors = colors;
            disableButtons.Add( button );
        }

        foreach( var button in disableButtons )
        {
            button.SetInteractable( false );
        }

        ThisButton.Select();
    }

    private void CleanUp()
    {
        foreach( var button in _highlightButtons )
        {
            var colors = button.ThisButton.colors;
            colors.disabledColor = _originalColor;

            button.ThisButton.colors = colors;
        }

        _targets.Clear();
    }

    public void OnSelect( BaseEventData eventData )
    {
        Debug.Log( $"[Target Select] Selected Multiple Target Buttons!" );
        AudioController.Instance.PlaySFX( SoundEffect.ButtonSelect );
    }

    public void OnDeselect( BaseEventData eventData )
    {
        
    }

    public void OnSubmit( BaseEventData eventData )
    {
        var attacker = _targetSelect.Attacker;
        List<BattleUnit> targets = new();
        var move = _moveUsed;

        for( int i = 0; i < _targets.Count; i++ )
        {
            targets.Add( _targets[i].AssignedUnit );
        }

        CleanUp();
        _targetSelect.PopTargetSelectState();
        //--Submit to queue
        _battleSystem.SetMoveCommand( attacker, targets, move );
    }

    public void OnCancel( BaseEventData eventData )
    {
        CleanUp();
        _targetSelect.PopTargetSelectState();
    }
}
