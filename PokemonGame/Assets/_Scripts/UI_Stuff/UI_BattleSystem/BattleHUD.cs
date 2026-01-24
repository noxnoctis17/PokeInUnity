using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

public class BattleHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _currentHPText;
    [SerializeField] private TextMeshProUGUI _maxHPText;
    [SerializeField] private Image _battlePortrait;
    [SerializeField] private Image _type1Color, _type2Color;
    [SerializeField] private GameObject _severeStatusContainer;
    [SerializeField] private Image _severeStatusIcon;
    [SerializeField] private HPBar _hpBar;
    [SerializeField] private GameObject _expBar;
    private Pokemon _pokemon;
    private BattleUnit _battleUnit;

    private void Update(){
        //--Update HP
        if( _hpBar.IsUpdating && _currentHPText != null )
            _currentHPText.text = $"{_hpBar.RedHPSlider.value}";
    }

    public void SetData( Pokemon pokemon, BattleUnit battleUnit ){
        if( _pokemon != null ){
            _pokemon.OnStatusChanged        -= SetSevereStatus;
            // _pokemon.OnDisplayInfoChanged   -= UpdateHP;
        }

        _pokemon = pokemon;
        _battleUnit = battleUnit;

        _nameText.text = pokemon.NickName;
        _levelText.text = "" + pokemon.Level;

        _battlePortrait.sprite = _pokemon.PokeSO.Portrait_Normal;

        //--Set Type Colors
        SetColors();

        _hpBar.SetHP( pokemon.CurrentHP, pokemon.MaxHP );
        SetExp();

        if( _currentHPText != null)
        {
            _currentHPText.text = $"{_hpBar.RedHPSlider.value}";
            _maxHPText.text = $"/{_hpBar.RedHPSlider.maxValue}";
        }

        SetSevereStatus();
        _pokemon.OnStatusChanged        += SetSevereStatus;
        // _pokemon.OnDisplayInfoChanged   += UpdateHP;
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
            _severeStatusContainer.gameObject.SetActive( false );
            _battleUnit.PokeAnimator.SetStatusColor( Color.white );
            return;
        }

        _severeStatusIcon.sprite = StatusIconAtlas.StatusIcons[_pokemon.SevereStatus.ID].Icon;
        _severeStatusContainer.gameObject.SetActive( true );
        _battleUnit.PokeAnimator.SetStatusColor( StatusIconAtlas.StatusIcons[_pokemon.SevereStatus.ID].Color );
    }

    public void RefreshHUD(){
        _levelText.text = "" + _pokemon.Level;
        _hpBar.SetHP( _pokemon.CurrentHP, _pokemon.MaxHP );
        
        if( _currentHPText != null )
        {
            _currentHPText.text = $"{_hpBar.RedHPSlider.value}";
            _maxHPText.text = $"/{_hpBar.RedHPSlider.maxValue}";
        }
    }

    private void ClearData(){
        _pokemon.OnStatusChanged        -= SetSevereStatus;
        // _pokemon.OnDisplayInfoChanged   -= UpdateHP;
        BattleSystem.OnBattleEnded      -= ClearData;
    }

    private void SetColors(){
        var type1 = _pokemon.PokeSO.Type1;
        var type2 = _pokemon.PokeSO.Type2;

        if( TypeColorsDB.TypeColors.ContainsKey( type1 ) ){
            _type1Color.color = TypeColorsDB.TypeColors[type1].PrimaryColor;
        }
        else{
            _type1Color.color = Color.white;
        }

        if( TypeColorsDB.TypeColors.ContainsKey( type2 ) ){
            _type2Color.color = TypeColorsDB.TypeColors[type2].SecondaryColor;    
        }
        else{
            if( type2 == PokemonType.None )
                _type2Color.color = TypeColorsDB.TypeColors[type1].SecondaryColor;
            else
            _type2Color.color = Color.black;
        }
    }

}
