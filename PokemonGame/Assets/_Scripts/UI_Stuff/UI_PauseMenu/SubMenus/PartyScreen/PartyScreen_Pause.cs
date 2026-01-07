using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using NoxNoctisDev.StateMachine;
using DG.Tweening;
using TMPro;
using System.Collections.Generic;

public class PartyScreen_Pause : State<UI_PauseMenuStateMachine>, IPartyScreen
{
    public UI_PauseMenuStateMachine StateMachine { get; private set; }
    [SerializeField] private PartyDisplay _partyDisplay;
    [SerializeField] private PokemonOptionMenu _pokemonOptionMenu; //--This is a state
    [SerializeField] private PartyScreen_PokemonEditor _pokemonEditor;
    [SerializeField] private PartyScreen_MoveEditor _moveEditor;
    private FollowerPokemon _follower;
    public bool IsSwitchingPokemon { get; private set; }
    public Pokemon EditingPokemon { get; private set; }
    private Button _initialButton;
    public PartyDisplay PartyDisplay => _partyDisplay;
    public PokemonOptionMenu PokemonOptionMenu => _pokemonOptionMenu;
    public PartyScreen_PokemonEditor PokemonEditor => _pokemonEditor;
    public PartyScreen_MoveEditor MoveEditor => _moveEditor;
    public Button LastButton { get; private set; }
    public Button InitialButton => _initialButton;
//======================================================================
    [Header( "Pokemon Information" )]
    [SerializeField] private TextMeshProUGUI _hpText, _atkText, _defText, _spatkText, _spdefText, _speText, _abilityText, _abilityDescription, _heldItemText;
    [SerializeField] private TextMeshProUGUI _expText, _epText;
    [SerializeField] private TextMeshProUGUI _hpEP, _atkEP, _defEP, _spatkEP, _spdefEP, _speEP;
    [SerializeField] private Slider _expBar;
    [SerializeField] private List<MoveButton_Summary> _moveDispay;
    [SerializeField] private Image _pokemonSprite;
    [SerializeField] private Image _heldItemIcon;
    [SerializeField] private Image _statusIcon;
    [SerializeField] private SimpleAnimator _simpleAnimator;

    public override void EnterState( UI_PauseMenuStateMachine owner )
    {
        Debug.Log( "EnterState: " + this );
        //--Set State Machine
        StateMachine = owner;
        _follower = PlayerReferences.Instance.FollowerPokemon;

        //--Open Menu
        gameObject.SetActive( true );

        //--Select Initial Button
        _initialButton = _partyDisplay.PartyButton1;
        StartCoroutine( SetInitialButton() );
    }

    public override void ReturnToState()
    {
        _partyDisplay.SetPartyButtons_Interactable( true );
        StartCoroutine( SetInitialButton() );
    }

    public override void PauseState()
    {
        _partyDisplay.SetPartyButtons_Interactable( false );
    }

    public override void ExitState()
    {
        StartCoroutine( ExitMenuAnim() );
    }

    private IEnumerator ExitMenuAnim()
    {
        yield return null;
        gameObject.SetActive( false );
    }

    private IEnumerator SetInitialButton()
    {
        yield return new WaitForSeconds( 0.15f );

        if( LastButton != null )
            SelectMemoryButton();
        else{
            SetAsAndSelectMemoryButton( _initialButton );
        }
    }

    public void SetAsLastButton( Button button )
    {
        LastButton = button;
    }

    public void SetAsAndSelectMemoryButton( Button lastButton )
    {
        LastButton = lastButton;
        SelectMemoryButton();
    }

    private void SelectMemoryButton()
    {
        LastButton.Select();
    }

    public void ClearMemoryButton()
    {
        LastButton = null;
        _initialButton.Select();
    }

    public void SetDisplayedPokemon( Pokemon pokemon )
    {
        //--Set Sprite
        _simpleAnimator.SetSpriteSheet( pokemon.PokeSO.IdleDownSprites );

        //--Set Stats. Going through the stat dictionary returns the raw calculated stat without boosts or modifiers, which are applied in GetStat()
        UpdateStats( pokemon );

        //--Set Exp
        SetExp( pokemon );
        _expText.text = $"{pokemon.Exp}/{pokemon.PokeSO.GetExpForLevel( Mathf.Min( pokemon.Level + 1, 100 ) )}";

        //--Set Effort Points
        UpdateEVs( pokemon );

        //--Set Status
        _statusIcon.gameObject.SetActive( false );
        if( pokemon.SevereStatus != null )
        {
            _statusIcon.sprite = StatusIconAtlas.StatusIcons[pokemon.SevereStatus.ID].Icon;
            _statusIcon.gameObject.SetActive( true );
        }

        //--Set Ability
        if( pokemon.Ability != null )
        {
            _abilityText.text = $"{pokemon.Ability.Name}";
            _abilityDescription.text =$"{pokemon.Ability.Description}";
        }

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
            _heldItemIcon.sprite = pokemon.HeldItem.Icon;
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

    public void SetFollowerPokemon( Pokemon follower )
    {   
        if( StateMachine.CurrentState == this )
            StateMachine.PopState();
            
        _follower.SetFollowerPokemon( follower );
    }

    public void SetSwitchingPokemonState( bool value )
    {
        IsSwitchingPokemon = value;
    }

    public void SetExp( Pokemon pokemon ){
        if( _expBar == null )
            return;

        float normalizedExp = GetNormalizedExp( pokemon );
        _expBar.maxValue = pokemon.PokeSO.GetExpForLevel( pokemon.Level );
        _expBar.value = normalizedExp;
    }

    private float GetNormalizedExp( Pokemon pokemon ){
        int currentLevelExp = pokemon.PokeSO.GetExpForLevel( pokemon.Level );
        int nextLevelExp = pokemon.PokeSO.GetExpForLevel( pokemon.Level + 1 );

        float normalizedExp = (float)( pokemon.Exp - currentLevelExp ) / ( nextLevelExp - currentLevelExp );

        return Mathf.Clamp01( normalizedExp );
    }

    public void UpdateStats( Pokemon pokemon )
    {
        _hpText.text        = $"{pokemon.Stats[Stat.HP]}";
        _atkText.text       = $"{pokemon.Stats[Stat.Attack]}";
        _defText.text       = $"{pokemon.Stats[Stat.Defense]}";
        _spatkText.text     = $"{pokemon.Stats[Stat.SpAttack]}";
        _spdefText.text     = $"{pokemon.Stats[Stat.SpDefense]}";
        _speText.text       = $"{pokemon.Stats[Stat.Speed]}";
    }

    public void UpdateEVs( Pokemon pokemon )
    {
        _hpEP.text        = $"{pokemon.HP_EVs}";
        _atkEP.text       = $"{pokemon.ATK_EVs}";
        _defEP.text       = $"{pokemon.DEF_EVs}";
        _spatkEP.text     = $"{pokemon.SPATK_EVs}";
        _spdefEP.text     = $"{pokemon.SPDEF_EVs}";
        _speEP.text       = $"{pokemon.SPE_EVs}";

        _epText.text = $"{pokemon.RemainingEffortPoints}/{pokemon.GainedEffortPoints}";
    }

    public void EditPokemon( Pokemon pokemon )
    {
        EditingPokemon = pokemon;
        StateMachine.PushState( _pokemonEditor );
    }

}
