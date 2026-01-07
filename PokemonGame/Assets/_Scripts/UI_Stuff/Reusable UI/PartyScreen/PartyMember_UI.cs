using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PartyMember_UI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _currentHPText;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private GameObject _statusContainer;
    [SerializeField] private GameObject _hpContainer;
    [SerializeField] private GameObject _canEvolveText;
    [SerializeField] private HPBar _hpBar;
    [SerializeField] private Image _statusBackground;
    [SerializeField] private Image _battlePortrait;
    [SerializeField] private Image _severeStatusIcon;
    [SerializeField] private Image _currenBall;
    [SerializeField] private Image _heldItemIcon;
    private int _currentHPTracker;
    private Pokemon _pokemon;
    public int HP => _currentHPTracker;
    public Pokemon Pokemon => _pokemon;

    public void Init( Pokemon pokemon ){
        _pokemon = pokemon;
        UpdateData();

        _pokemon.OnDisplayInfoChanged   += UpdateData;
        _pokemon.OnStatusChanged        += UpdateStatusCondition;
    }

    private void Update(){
        if( _currentHPTracker != _hpBar.RedHPSlider.value )
            _currentHPText.text = $"{_hpBar.RedHPSlider.value}/{_hpBar.RedHPSlider.maxValue}";
    }

    private void UpdateData(){
        _nameText.text = _pokemon.NickName;
        _levelText.text = $"Lv. {_pokemon.Level}";
        _hpBar.SetHP( _pokemon.CurrentHP, _pokemon.MaxHP );
        _currentHPTracker = _pokemon.CurrentHP;
        _currentHPText.text = $"{_hpBar.RedHPSlider.value}/{_hpBar.RedHPSlider.maxValue}";
        _statusText.text = "";

        _battlePortrait.sprite = _pokemon.PokeSO.CardPortrait;

        _currenBall.sprite = _pokemon.CurrentBallSprite;

        _heldItemIcon.gameObject.SetActive( false );
        if( _pokemon.HeldItem != null )
        {
            _heldItemIcon.sprite = _pokemon.HeldItem.Icon;
            _heldItemIcon.gameObject.SetActive( true );
        }

        if( _pokemon.CanEvolveByLevelUp && _canEvolveText != null )
            _canEvolveText.SetActive( true );
        else if( !_pokemon.CanEvolveByLevelUp && _canEvolveText != null )
            _canEvolveText.SetActive( false );
    }

    public void UpdateStatusText_EvoItem( Item item, bool showStatus = false ){
        if( _pokemon == null )
            return;

        if( item != null ){
            var evoItem = (EvolutionItemsSO)item.ItemSO;

            if( _pokemon.CheckForEvolution( evoItem ) != null ){
                _statusBackground.color = Color.green;
                _statusText.text = "Compatible!";
            }
            else{
                _statusBackground.color = Color.red;
                _statusText.text = "Incompatible!";
            }
        }

        _statusContainer.SetActive( showStatus );
    }

    public void UpdateStatusText_TM( Item item, bool showStatus = false ){
        if( _pokemon == null )
            return;

        if( item != null ){
            var tm = (TMItemSO)item.ItemSO;

            if( _pokemon.CheckHasMove( tm.MoveSO ) ){
                _statusBackground.color = Color.blue;
                _statusText.text = "Learned";
            }
            else if( _pokemon.CheckCanLearnMove( tm.MoveSO ) ){
                _statusBackground.color = Color.green;
                _statusText.text = "Can Learn";
            }
            else{
                _statusBackground.color = Color.red;
                _statusText.text = "Incompatible!";
            }
        }

        _statusContainer.SetActive( showStatus );
    }

    public void ShowHPBar( bool showBar ){
        _hpContainer.SetActive( showBar );
    }

    private void UpdateStatusCondition(){
        // Debug.Log( "PartyMember_UI: UpdateStatus()" );
        if( _pokemon.SevereStatus != null ){
            var status = _pokemon.SevereStatus.ID;
            _severeStatusIcon.sprite = StatusConditionsDB.Conditions[status].StatusIcon;
            _severeStatusIcon.gameObject.SetActive( true );
        }
        else
            _severeStatusIcon.gameObject.SetActive( false );
    }

    public void AnimateBall( Vector3 rotate )
    {
        _currenBall.GetComponent<RectTransform>().DOLocalRotate( rotate, 0.25f );
    }
}
