using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

public class BattleHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _hpText;
    [SerializeField] private Image _battlePortrait;
    [SerializeField] private Image _type1Color, _type2Color;
    [SerializeField] private GameObject _severeStatusContainer;
    [SerializeField] private Image _severeStatusIcon;
    [SerializeField] private HPBar _hpBar;
    [SerializeField] private GameObject _expBar;
    [SerializeField] private bool _displayPercentage;
    private bool _finishedUpdatingHP;
    private Pokemon _pokemon;
    private BattleUnit _battleUnit;

    private void Update()
    {
        //--Update HP
        if( _hpBar.IsUpdating && _hpText != null )
        {
            UpdateHPText();
            _finishedUpdatingHP = true;
        }

        if( _finishedUpdatingHP && !_hpBar.IsUpdating )
        {
            UpdateHPText();
            _finishedUpdatingHP = false;
        }
    }

    public void SetData( Pokemon pokemon, BattleUnit battleUnit )
    {
        if( _pokemon != null )
            _pokemon.OnStatusChanged -= SetSevereStatus;

        _pokemon = pokemon;
        _battleUnit = battleUnit;

        _nameText.text = pokemon.NickName;
        _levelText.text = "" + pokemon.Level;

        _battlePortrait.sprite = _pokemon.PokeSO.Portrait_Normal;

        //--Set Type Colors
        SetColors();

        _hpBar.SetHP( pokemon.CurrentHP, pokemon.MaxHP );
        SetExp();

        UpdateHPText();

        SetSevereStatus();
        _pokemon.OnStatusChanged        += SetSevereStatus;
        BattleSystem.OnBattleEnded      += ClearData;
    }

    private void UpdateHPText()
    {
        if( _displayPercentage )
        {
            float current = _hpBar.RedHPSlider.value;
            float max = _hpBar.RedHPSlider.maxValue;
            float percent = Mathf.Floor( ( current / max ) * 100f );

            if( percent < 1f && current > 0f )
                percent = 1f;
            
            if( _hpBar.IsUpdating )
                _hpText.text = $"<size=125%>{percent}<size=65%>%</size>";
            else
                _hpText.text = $"{percent}<size=65%>%</size>";
        }
        else
        {
            if( _hpBar.IsUpdating )
                _hpText.text = $"<size=125%>{_hpBar.RedHPSlider.value}<size=65%>/{_hpBar.RedHPSlider.maxValue}</size>";
            else
                _hpText.text = $"{_hpBar.RedHPSlider.value}<size=65%>/{_hpBar.RedHPSlider.maxValue}</size>";
        }
    }

    public IEnumerator UpdateHPCoroutine()
    {
        if( _hpBar.RedHPSlider.value < _pokemon.CurrentHP )
            _battlePortrait.sprite = _pokemon.PokeSO.Portrait_Happy;
        else
            _battlePortrait.sprite = _pokemon.PokeSO.Portrait_Hurt;

        yield return _hpBar.AnimateHP( _pokemon.CurrentHP );
        yield return null;

        if( _pokemon.IsBelowHPPercent( 34 ) )
            _battlePortrait.sprite = _pokemon.PokeSO.Portrait_Angry;
        else
            _battlePortrait.sprite = _pokemon.PokeSO.Portrait_Normal;
    }

    public IEnumerator WaitForHPUpdate()
    {
        yield return new WaitUntil( () => _hpBar.IsUpdating == false );
    }

    public void SetExp()
    {
        if( _expBar == null )
            return;

        float normalizedExp = GetNormalizedExp();
        _expBar.transform.localScale = new Vector3( 1, normalizedExp, 1 );
    }

    public IEnumerator SetExpSmooth( bool reset = false )
    {
        if( _expBar == null )
            yield break;

        if( reset )
            _expBar.transform.localScale = new Vector3( 1, 0, 1 );

        float normalizedExp = GetNormalizedExp();
        yield return _expBar.transform.DOScaleY( normalizedExp, 1.5f ).WaitForCompletion();
    }

    private float GetNormalizedExp()
    {
        int currentLevelExp = _pokemon.PokeSO.GetExpForLevel( _pokemon.Level );
        int nextLevelExp = _pokemon.PokeSO.GetExpForLevel( _pokemon.Level + 1 );

        float normalizedExp = (float)( _pokemon.Exp - currentLevelExp ) / ( nextLevelExp - currentLevelExp );

        return Mathf.Clamp01( normalizedExp );
    }

    private void SetSevereStatus()
    {
        if( _pokemon.SevereStatus == null )
        {
            _severeStatusContainer.SetActive( false );
            _battleUnit.PokeAnimator.SetStatusColor( Color.white );
            return;
        }

        if( _pokemon.IsFainted )
        {
            _severeStatusIcon.sprite = StatusIconAtlas.FNT;
            _battleUnit.PokeAnimator.SetStatusColor( StatusIconAtlas.FNTColor );
            return;
        }

        _severeStatusIcon.sprite = StatusIconAtlas.StatusIcons[_pokemon.SevereStatus.ID].Icon;
        _severeStatusContainer.SetActive( true );
        _battleUnit.PokeAnimator.SetStatusColor( StatusIconAtlas.StatusIcons[_pokemon.SevereStatus.ID].Color );
    }

    public void RefreshHUD()
    {
        _levelText.text = "" + _pokemon.Level;
        _hpBar.SetHP( _pokemon.CurrentHP, _pokemon.MaxHP );
        
        UpdateHPText();
    }

    private void ClearData()
    {
        _pokemon.OnStatusChanged        -= SetSevereStatus;
        BattleSystem.OnBattleEnded      -= ClearData;
    }

    private void SetColors()
    {
        var type1 = _pokemon.PokeSO.Type1;
        var type2 = _pokemon.PokeSO.Type2;

        if( TypeColorsDB.TypeColors.ContainsKey( type1 ) )
            _type1Color.color = TypeColorsDB.TypeColors[type1].PrimaryColor;
        else
            _type1Color.color = Color.white;

        if( TypeColorsDB.TypeColors.ContainsKey( type2 ) && type2 != PokemonType.None )
            _type2Color.color = TypeColorsDB.TypeColors[type2].SecondaryColor;    
        else
        {
            if( type2 == PokemonType.None )
                _type2Color.color = TypeColorsDB.TypeColors[type1].SecondaryColor;
            else
                _type2Color.color = Color.black;
        }
    }

}
