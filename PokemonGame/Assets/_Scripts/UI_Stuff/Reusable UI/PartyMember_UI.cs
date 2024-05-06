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
    public int HP => _currentHPTracker;

    private Pokemon _pokemon;
    public Pokemon Pokemon => _pokemon;

    private void Update(){
        if( _currentHPTracker != _hpBar.hpBar.value )
        _currentHPText.text = $"{_hpBar.hpBar.value}/{_hpBar.hpBar.maxValue}";
    }

    public void SetData( Pokemon pokemon ){
        _pokemon = pokemon;

        _nameText.text = pokemon.PokeSO.pName;
        _levelText.text = "" + pokemon.Level;
        _battlePortrait.sprite = _pokemon.PokeSO.BattlePortrait;
        _hpBar.SetHP( pokemon.CurrentHP, pokemon.MaxHP );
        _currentHPTracker = pokemon.CurrentHP;
        _currentHPText.text = $"{_hpBar.hpBar.value}/{_hpBar.hpBar.maxValue}";
    }

}
