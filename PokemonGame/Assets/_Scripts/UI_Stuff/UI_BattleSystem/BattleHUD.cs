using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

public class BattleHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private Image _battlePortrait;
    [SerializeField] private Image _type1Color, _type2Color;
    [SerializeField] private HPBar _hpBar;
    [SerializeField] private GameObject _expBar;
    [SerializeField] private TextMeshProUGUI _currentHPText;
    [SerializeField] private Image _severeStatusIcon;
    private Pokemon _pokemon;
    private BattleUnit _battleUnit;
    private int _currentHPTracker;
    public int CurrentHPTracker => _currentHPTracker;

    private void Update(){
        //--Update HP
        if( _currentHPTracker != _hpBar.hpBar.value )
            _currentHPText.text = $"{_hpBar.hpBar.value}/{_hpBar.hpBar.maxValue}";

        if( _pokemon != null && _pokemon == BattleSystem.Instance.WildPokemon )
            UpdateWildHUDPosition();
    }

    public void SetData( Pokemon pokemon, BattleUnit battleUnit ){
        if( _pokemon != null ){
            _pokemon.OnStatusChanged        -= SetSevereStatus;
            _pokemon.OnDisplayInfoChanged   -= UpdateHP;
        }

        _pokemon = pokemon;
        _battleUnit = battleUnit;

        _nameText.text = pokemon.PokeSO.Name;
        _levelText.text = "" + pokemon.Level;

        if( _pokemon.IsPlayerUnit )
            _battlePortrait.sprite = _pokemon.PokeSO.CardPortrait;

        //--Set Type Colors
        if( _type1Color || _type2Color != null ){
            SetColors();
        }

        _hpBar.SetHP( pokemon.CurrentHP, pokemon.MaxHP );
        SetExp();

        _currentHPTracker = pokemon.CurrentHP;
        _currentHPText.text = $"{_hpBar.hpBar.value}/{_hpBar.hpBar.maxValue}";

        SetSevereStatus();
        _pokemon.OnStatusChanged        += SetSevereStatus;
        _pokemon.OnDisplayInfoChanged   += UpdateHP;
        BattleSystem.OnBattleEnded      += ClearData;
    }

    private void UpdateHP(){
        StartCoroutine( UpdateHPCoroutine() );
    }

    public IEnumerator UpdateHPCoroutine(){
        yield return _hpBar.AnimateHP( _pokemon.CurrentHP );
    }

    public IEnumerator WaitForHPUpdate(){
        yield return new WaitUntil( () => _hpBar.IsUpdating == false );
    }

    public void SetExp(){
        if( _expBar == null )
            return;

        float normalizedExp = GetNormalizedExp();
        _expBar.transform.localScale = new Vector3( 1, normalizedExp, 1 );
    }

    public IEnumerator SetExpSmooth( bool reset = false ){
        if( _expBar == null )
            yield break;

        if( reset )
            _expBar.transform.localScale = new Vector3( 1, 0, 1 );

        float normalizedExp = GetNormalizedExp();
        yield return _expBar.transform.DOScaleY( normalizedExp, 1.5f ).WaitForCompletion();
    }

    private float GetNormalizedExp(){
        int currentLevelExp = _pokemon.PokeSO.GetExpForLevel( _pokemon.Level );
        int nextLevelExp = _pokemon.PokeSO.GetExpForLevel( _pokemon.Level + 1 );

        float normalizedExp = (float)( _pokemon.Exp - currentLevelExp ) / ( nextLevelExp - currentLevelExp );

        return Mathf.Clamp01( normalizedExp );
    }

    private void SetSevereStatus(){
        if( _pokemon.SevereStatus == null ){
            _severeStatusIcon.gameObject.SetActive( false );
            _battleUnit.PokeAnimator.SetStatusColor( Color.white );
            return;
        }

        _severeStatusIcon.gameObject.SetActive( true );
        _severeStatusIcon.sprite = StatusIconAtlas.StatusIcons[_pokemon.SevereStatus.ID].icon;
        _battleUnit.PokeAnimator.SetStatusColor( StatusIconAtlas.StatusIcons[_pokemon.SevereStatus.ID].color );
    }

    public void RefreshHUD(){
        _levelText.text = "" + _pokemon.Level;
        _hpBar.SetHP( _pokemon.CurrentHP, _pokemon.MaxHP );
        _currentHPText.text = $"{_hpBar.hpBar.value}/{_hpBar.hpBar.maxValue}";
    }

    private void ClearData(){
        _pokemon.OnStatusChanged    -= SetSevereStatus;
        _pokemon.OnDisplayInfoChanged        -= UpdateHP;
        BattleSystem.OnBattleEnded  -= ClearData;
    }

    private void SetColors(){
        var type1 = _pokemon.PokeSO.Type1;
        var type2 = _pokemon.PokeSO.Type2;

        if( TypeColorsDB.TypeColors.ContainsKey( type1 ) && TypeColorsDB.TypeColors.ContainsKey( type2 ) ){
            _type1Color.color = TypeColorsDB.TypeColors[type1].PrimaryColor;

            if( type2 == PokemonType.None )
                _type2Color.color = TypeColorsDB.TypeColors[type1].SecondaryColor;
            else
                _type2Color.color = TypeColorsDB.TypeColors[type2].SecondaryColor;
        }
    }

    private void UpdateWildHUDPosition(){
        var wildPokemonPos = BattleSystem.Instance.EncounteredPokemon.gameObject.transform.position;
        var pos = new Vector3( wildPokemonPos.x, wildPokemonPos.y + 2.5f, wildPokemonPos.z );

        transform.position = pos;
    }

}
