using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class BattleHUD : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _nameText;
    [SerializeField] TextMeshProUGUI _levelText;
    [SerializeField] Image _battlePortrait;
    [SerializeField] HPBar _hpBar;
    [SerializeField] PPBar _ppBar;
    [SerializeField] TextMeshProUGUI _currentHPText;
    [SerializeField] TextMeshProUGUI _currentPPText;
    [SerializeField] Sprite _psnIcon, _toxIcon, _brnIcon, _parIcon, _slpIcon, _frzIcon, _fntIcon;
    [SerializeField] Image _severeStatusIcon;
    private int _currentHPTracker;
    public int CurrentHPTracker => _currentHPTracker;
    private int _currentPPTracker;
    public int CurrentPPTracker => _currentPPTracker;

    private PokemonClass _pokemon;
    private Dictionary<ConditionID, Sprite> _severeStatusIcons;

    private void Update()
    {
        //--Update HP
        if(_currentHPTracker != _hpBar.hpBar.value)
        _currentHPText.text = $"{_hpBar.hpBar.value}/{_hpBar.hpBar.maxValue}";

        //--Update PP
        if(_currentPPTracker != _ppBar.ppBar.value)
        _currentPPText.text = $"{_ppBar.ppBar.value}/{_ppBar.ppBar.maxValue}";
        
    }
    public void SetData(PokemonClass pokemon)
    {
        _pokemon = pokemon;

        _nameText.text = pokemon.PokeSO.pName;
        _levelText.text = "" + pokemon.Level;

        _battlePortrait.sprite = _pokemon.PokeSO.BattlePortrait;
        //--Set type icons

        _hpBar.SetHP(pokemon.PokeSO, pokemon.currentHP, pokemon.MaxHP);
        _ppBar.SetPP(pokemon.PokeSO, pokemon.currentPP, pokemon.MaxPP);

        _currentHPTracker = pokemon.currentHP;
        _currentHPText.text = $"{_hpBar.hpBar.value}/{_hpBar.hpBar.maxValue}";

        _currentPPTracker = pokemon.currentPP;
        _currentPPText.text = $"{_ppBar.ppBar.value}/{_ppBar.ppBar.maxValue}";

        _severeStatusIcons = new Dictionary<ConditionID, Sprite>()
        {
            { ConditionID.PSN, _psnIcon },
            { ConditionID.TOX, _toxIcon },
            { ConditionID.BRN, _brnIcon },
            { ConditionID.PAR, _parIcon },
            { ConditionID.SLP, _slpIcon },
            { ConditionID.FRST, _frzIcon },
            { ConditionID.FNT, _fntIcon }
        };

        SetSevereStatusIcon();
        _pokemon.OnStatusChanged += SetSevereStatusIcon;
    }

    public IEnumerator UpdateHP()
    {
        if(_pokemon.HPChanged)
        {
            yield return _hpBar.AnimateHP(_pokemon.PokeSO, _pokemon.currentHP);
            _pokemon.HPChanged = false;
        }
        
    }
    
    public IEnumerator UpdatePP()
    {
        yield return _ppBar.AnimatePP(_pokemon.PokeSO, _pokemon.currentPP);
    }

    private void SetSevereStatusIcon()
    {
        if(_pokemon.SevereStatus == null)
        {
            _severeStatusIcon.gameObject.SetActive(false);
            return;
        }

        _severeStatusIcon.gameObject.SetActive(true);
        _severeStatusIcon.sprite = _severeStatusIcons[_pokemon.SevereStatus.ID];

    }

}
