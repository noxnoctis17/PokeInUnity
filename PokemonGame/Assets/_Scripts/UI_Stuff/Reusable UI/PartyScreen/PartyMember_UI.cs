using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PartyMember_UI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private Image _battlePortrait;
    [SerializeField] private HPBar _hpBar;
    [SerializeField] private TextMeshProUGUI _currentHPText;
    private int _currentHPTracker;
    private Pokemon _pokemon;
    public int HP => _currentHPTracker;
    public Pokemon Pokemon => _pokemon;

    public void Init( Pokemon pokemon ){
        _pokemon = pokemon;
        UpdateData();

        _pokemon.OnHpChanged += UpdateData;
    }

    private void OnDestroy(){
        _pokemon.OnHpChanged -= UpdateData;
    }

    private void Update(){
        if( _currentHPTracker != _hpBar.hpBar.value )
        _currentHPText.text = $"{_hpBar.hpBar.value}/{_hpBar.hpBar.maxValue}";
    }

    private void UpdateData(){
        _nameText.text = _pokemon.PokeSO.pName;
        _levelText.text = "" + _pokemon.Level;
        if( _pokemon.PokeSO.IdleDownSprites != null ) //--TODO: Remove, all mons should have sprites lol
            _battlePortrait.sprite = _pokemon.PokeSO.IdleDownSprites[0];
        _hpBar.SetHP( _pokemon.CurrentHP, _pokemon.MaxHP );
        _currentHPTracker = _pokemon.CurrentHP;
        _currentHPText.text = $"{_hpBar.hpBar.value}/{_hpBar.hpBar.maxValue}";
    }

}
