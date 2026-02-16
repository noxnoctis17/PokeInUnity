using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using NoxNoctisDev.StateMachine;
using TMPro;

public class PartyScreen_Battle : State<PlayerBattleMenu>, IPartyScreen
{
    [SerializeField] private BattleSystem _battleSystem;
    [SerializeField] private PartyDisplay _partyDisplay;
    [SerializeField] private PartyScreenContext _partyScreenContext;
    [SerializeField] private SimpleAnimator _simpleAnimator;
//=====================================================================
    [Header( "Pokemon Information" )]
    [SerializeField] private TextMeshProUGUI _hpText, _atkText, _defText, _spatkText, _spdefText, _speText, _abilityText, _heldItemText, _natureText;
    [SerializeField] private List<MoveButton_Summary> _moveDispay;
    [SerializeField] private Image _pokemonSprite;
    [SerializeField] private Image _heldItemIcon;
    [SerializeField] private Image _statusIcon;

//=====================================================================
    private Button _initialButton;
    public BattleSystem BattleSystem => _battleSystem;
    public PartyDisplay PartyDisplay => _partyDisplay;
    public PlayerBattleMenu BattleMenu { get; private set; }
    public Button LastButton { get; private set; }

    public override void EnterState( PlayerBattleMenu owner ){
        //--State Machine
        BattleMenu = owner;

        //--Open Menu
        gameObject.SetActive( true );

        //--Select Initial Button
        _initialButton = _partyDisplay.PartyButton1;
        StartCoroutine( SetInitialButton() );
    }

    public override void ReturnToState()
    {
        StartCoroutine( SetInitialButton() );
    }

    public override void ExitState(){
        gameObject.SetActive( false );
        ClearMoves();
    }

    private IEnumerator SetInitialButton(){
        yield return new WaitForSeconds( 0.15f );

        if( LastButton != null )
            SelectMemoryButton();
        else{
            SetMemoryButton( _initialButton );
        }
    }

    public void SetMemoryButton( Button lastButton ){
        LastButton = lastButton;
        SelectMemoryButton();
    }

    private void SelectMemoryButton(){
        LastButton.Select();
    }

    public void ClearMemoryButton(){
        LastButton = null;
        _initialButton.Select();
    }

    public void SetDisplayedPokemon( Pokemon pokemon )
    {
        //--Set Sprite
        // _pokemonSprite.sprite = pokemon.PokeSO.IdleDownSprites[0];
        _simpleAnimator.SetSpriteSheet( pokemon.PokeSO.IdleDownSprites );

        //--Set Stats. Going through the stat dictionary returns the raw calculated stat without boosts or modifiers, which are applied in GetStat()
        _hpText.text        = $"{pokemon.Stats[Stat.HP]}";
        _atkText.text       = $"{pokemon.Stats[Stat.Attack]}";
        _defText.text       = $"{pokemon.Stats[Stat.Defense]}";
        _spatkText.text     = $"{pokemon.Stats[Stat.SpAttack]}";
        _spdefText.text     = $"{pokemon.Stats[Stat.SpDefense]}";
        _speText.text       = $"{pokemon.Stats[Stat.Speed]}";

        //--Set Status
        _statusIcon.gameObject.SetActive( false );
        if( pokemon.SevereStatus != null )
        {
            _statusIcon.sprite = StatusIconAtlas.StatusIcons[pokemon.SevereStatus.ID].Icon;
            _statusIcon.gameObject.SetActive( true );
        }

        //--Set Ability
        if( pokemon.Ability != null )
            _abilityText.text = $"{pokemon.Ability.Name}";

        //--Set Held Item
        _heldItemIcon.gameObject.SetActive( false );
        if( pokemon.HeldItem != null )
        {
            _heldItemText.text = $"{pokemon.HeldItem.ItemName}";
            _heldItemIcon.sprite = pokemon.HeldItem.Icon;
            _heldItemIcon.gameObject.SetActive( true );
        }
        else
        {
            _heldItemText.text = $"-";
            _heldItemIcon.sprite = null;
            _heldItemIcon.gameObject.SetActive( false );
        }

        //--Set Moves
        SetMoveNames( pokemon.ActiveMoves );
    }

    private void SetMoveNames( List<Move> moves )
    {
        ClearMoves();

        for( int i = 0; i < moves.Count; i++ )
        {
            if( moves[i] != null )
            {
                _moveDispay[i].Setup( moves[i] );
                _moveDispay[i].gameObject.SetActive( true );
            }
        }
    }

    private void ClearMoves()
    {
        for( int i = 0; i < _moveDispay.Count; i++ )
        {
            _moveDispay[i].gameObject.SetActive( false );
        }
    }
}
