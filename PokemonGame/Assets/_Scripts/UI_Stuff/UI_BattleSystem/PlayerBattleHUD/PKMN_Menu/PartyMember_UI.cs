using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PartyMember_UI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _nameText;
    [SerializeField] TextMeshProUGUI _levelText;
    [SerializeField] Image _battlePortrait;
    [SerializeField] HPBar _hpBar;
    [SerializeField] TextMeshProUGUI _currentHPText;
    private int _currentHPTracker;
    public int HP => _currentHPTracker;

    private PokemonClass _pokemon;
    public PokemonClass Pokemon => _pokemon;

    private void Update(){
        if( _currentHPTracker != _hpBar.hpBar.value )
        _currentHPText.text = $"{_hpBar.hpBar.value}/{_hpBar.hpBar.maxValue}";
    }

    public void SetData( PokemonClass pokemon ){
        _pokemon = pokemon;

        _nameText.text = pokemon.PokeSO.pName;
        _levelText.text = "" + pokemon.Level;
        _battlePortrait.sprite = _pokemon.PokeSO.BattlePortrait;
        _hpBar.SetHP( pokemon.CurrentHP, pokemon.MaxHP );
        _currentHPTracker = pokemon.CurrentHP;
        _currentHPText.text = $"{_hpBar.hpBar.value}/{_hpBar.hpBar.maxValue}";
    }

}
