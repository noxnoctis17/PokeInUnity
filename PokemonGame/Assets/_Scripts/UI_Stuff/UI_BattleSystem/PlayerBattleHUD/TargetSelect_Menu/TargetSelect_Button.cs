using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TargetSelect_Button : MonoBehaviour, ISelectHandler, IDeselectHandler, ICancelHandler, ISubmitHandler
{
    private BattleSystem _battleSystem;
    [SerializeField] private BattleMenu_TargetSelectState _targetSelectState;
    [SerializeField] private Image _portrait;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _hpText;
    private BattleUnit _attacker;
    private Button _thisButton;
    private Move _moveToBeUsed;
    public Button ThisButton => _thisButton;
    public BattleUnit AssignedUnit { get; private set; }

    public void Setup( BattleUnit assignedUnit, BattleUnit attacker, Move move )
    {
        _thisButton = GetComponent<Button>();
        AssignedUnit = assignedUnit;
        _attacker = attacker;
        _moveToBeUsed = move;
        _battleSystem = _targetSelectState.PlayerBattleMenu.BattleSystem;

        if( AssignedUnit.Pokemon != null )
        {
            _portrait.sprite = AssignedUnit.PokeSO.Portrait_Normal;
            _nameText.text = AssignedUnit.Pokemon.NickName;
            _levelText.text = $"Lv. {AssignedUnit.Pokemon.Level}";
            _hpText.text = $"{AssignedUnit.Pokemon.CurrentHP}/{AssignedUnit.Pokemon.MaxHP}";
        }
        
        SetInteractable( false );
    }

    public void SetInteractable( bool value )
    {
        ThisButton.interactable = value;
    }

    public void OnSelect( BaseEventData eventData )
    {
        AudioController.Instance.PlaySFX( SoundEffect.ButtonSelect );
    }

    public void OnDeselect( BaseEventData eventData )
    {

    }

    public void OnSubmit( BaseEventData eventData )
    {   
        List<BattleUnit> target = new() { AssignedUnit };
        _targetSelectState.PlayerBattleMenu.PopState();
        _battleSystem.SetMoveCommand( _attacker, target, _moveToBeUsed );
    }

    public void OnCancel( BaseEventData eventData )
    {
        _targetSelectState.PlayerBattleMenu.PopState();
    }

}
