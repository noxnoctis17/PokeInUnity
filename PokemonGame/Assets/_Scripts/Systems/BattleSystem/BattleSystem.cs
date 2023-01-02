using System.Collections;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public enum BattleStateEnum { Start, PlayerAction, Busy, NextTurn, SelectingNextPokemon, Over }

public enum BattleType { WildBattle1v1, WildBattle2v2, Trainer1v1, Trainer2v2, Trainer3v3 }

[Serializable]
public class BattleSystem : BattleStateMachine
{
    private BattleType _battleType;
    public BattleType BattleType => _battleType;
    [SerializeField] private GameObject _battleUnitPrefab;
    public GameObject BattleUnitPrefab => _battleUnitPrefab;
    [SerializeField] private BattleUnit _playerUnit => PlayerUnit;
    public BattleUnit PlayerUnit { get; set; }
    [SerializeField] private BattleUnit _enemyUnit;
    public BattleUnit EnemyUnit => _enemyUnit;
    [SerializeField] private BattleHUD _playerHUD;
    public BattleHUD PlayerHUD => _playerHUD;
    [SerializeField] private BattleHUD _enemyHUD;
    public BattleHUD EnemyHUD => _enemyHUD;
    [SerializeField] private BattleDialogueBox _dialogueBox;
    public BattleDialogueBox DialogueBox => _dialogueBox;
    public Queue<BattleDialogueBox> DialogueBoxeUpdates { get; set; }
    [SerializeField] private Transform _damageTakenPopupPrefab;
    public static Transform DamageTakenPopupPrefab;
    [SerializeField] private PlayerBattleMenu _battleMenu;
    [SerializeField] private FightMenu _fightMenu;
    public FightMenu FightMenu => _fightMenu;
    [SerializeField] private PKMNMenu _pkmnMenu;
    public PKMNMenu PKMNMenu => _pkmnMenu;
    [SerializeField] private PartyScreen _partyScreen;
    public PartyScreen PartyScreen => _partyScreen;
    [SerializeField] private EventSystem _eventSystem;
    public EventSystem EventSystem => _eventSystem;
    [SerializeField] private BattleSceneSetup _battleSceneSetup;
    public BattleSceneSetup BattleSceneSetup => _battleSceneSetup;
    [SerializeField] private AudioSource _audioSource;
    public AudioSource AudioSource => _audioSource;

//----------------------------------------------------------------------------

    private BattleStateEnum _battleStateEnum;
    public BattleStateEnum BattleStateEnum => _battleStateEnum;
    public static Action OnBattleStarted;
    public static Action OnBattleEnded;
    public static Action OnPlayerCommandSelect;
    public static Action OnPlayerAction;
    public static Action OnPlayerPokemonFainted;
    public static Action OnPlayerChoseNextPokemon;

//----------------------------------------------------------------------------
    private int _turnsLeft;
    private bool _isFainted;
    private bool _isFaintedSwitch;

    private PokemonParty _playerParty;
    public PokemonParty PlayerParty => _playerParty;
    private int _playerUnitAmount;
    public int PlayerUnitAmount => _playerUnitAmount;
    private PokemonClass _wildPokemon;
    public PokemonClass WildPokemon => _wildPokemon;

//----------------------------------Command System-----------------------------

    private List<IBattleCommand> _commandList;
    private Queue<IBattleCommand> _commandQueue;
    private UseMoveCommand _useMoveCommand;
    private SwitchPokemonCommand _switchPokemonCommand;
    private RunFromBattleCommand _runFromBattleCommand;

//----------------------------------------------------------------------
//----------------------------------------------------------------------
//----------------------------------------------------------------------

    private void OnEnable(){
        GameStateTemp.GameState = GameState.Battle;
        GameStateTemp.OnGameStateChanged?.Invoke();
        DamageTakenPopupPrefab = _damageTakenPopupPrefab;
    }

    private void OnDisable(){
        GameStateTemp.GameState = GameState.Overworld;
        GameStateTemp.OnGameStateChanged?.Invoke();
    }

    private void Start()    {
        _commandQueue = new Queue<IBattleCommand>();
        _commandList = new List<IBattleCommand>();
        DialogueBoxeUpdates = new Queue<BattleDialogueBox>();
    }

    private void Update(){
        switch( _battleStateEnum ){
            case BattleStateEnum.Busy :

                if( _eventSystem.enabled ){
                    _eventSystem.enabled = false;
                    BattleUIActions.OnBattleSystemBusy?.Invoke();
                } 
                break;

            case BattleStateEnum.PlayerAction :

                if( !_eventSystem.enabled ){
                    _eventSystem.enabled = true;
                    OnPlayerAction?.Invoke();
                }
                break;

            case BattleStateEnum.SelectingNextPokemon :

                if( !_eventSystem.enabled ){
                    _eventSystem.enabled = true;
                }
                break;
        }
    }

    public void StartWildBattle( PokemonParty playerParty, PokemonClass wildPokemon ){
        _playerParty = playerParty;
        _wildPokemon = wildPokemon;
        _battleType = BattleType.WildBattle1v1;

        switch( BattleType ){
            case BattleType.WildBattle1v1:

                _playerUnitAmount = 1;
                break;
        }
        
        SetState( new BattleState_Setup( this ) );
    }

    public void StartTrainerBattle( PokemonParty playerParty, PokemonParty enemyParty ){
        //--😈
    }

    public void PlayerAction(){
        _battleStateEnum = BattleStateEnum.PlayerAction;
    }

    private void OpenPartyMenu(){
        Debug.Log( "OpenPartyMenu Called" );
        _isFaintedSwitch = true;
        _pkmnMenu.gameObject.SetActive( enabled );
        _battleStateEnum = BattleStateEnum.SelectingNextPokemon;
        OnPlayerPokemonFainted?.Invoke();
        BattleUIActions.OnSubMenuOpened?.Invoke();
        BattleUIActions.OnPkmnMenuOpened?.Invoke();
    }
    
    public void ClosePartyMenu( PokemonClass pokemon ){
        var switchedTo = pokemon;
        _pkmnMenu.gameObject.SetActive( !enabled );
        BattleUIActions.OnSubMenuClosed?.Invoke();
        StartCoroutine( PerformSwitchPokemonCommand( switchedTo ) );
    }

//--------------------------------------------------------------------------------------------------------
//----------------------------------------------COMMANDS-------------------------------------------------
//--------------------------------------------------------------------------------------------------------

    public IEnumerator PerformMoveCommand( MoveClass move, BattleUnit attacker, BattleUnit target ){
        bool canAttack = attacker.Pokemon.OnBeforeTurn();
        Debug.Log(canAttack);
        if( !canAttack ){
            yield return attacker.BattleHUD.UpdateHP();
            yield break;
        }

        var moveEffects = move.moveBase.MoveEffects;
        attacker.Pokemon.currentPP = attacker.Pokemon.currentPP - move.PP;
        yield return attacker.BattleHUD.UpdatePP();
        yield return _dialogueBox.TypeDialogue( $"{attacker.Pokemon.PokeSO.pName} used {move.moveBase.MoveName}!" );

        // BattleUIActions.OnCommandUsed?.Invoke(); //--hide UI
        yield return new WaitForSeconds(0.5f); //--attack animation placeholder
        // BattleUIActions.OnCommandAnimationsCompleted?.Invoke(); //--restore ui
        // yield return new WaitForSeconds(1f); //--wait for ui to be on screen lol

        if( CheckMoveAccuracy( move, attacker, target ) ){
            if( move.moveBase.MoveCategory == MoveCategory.Status ){
                yield return RunMoveEffects( move.moveBase.MoveEffects, move.moveBase.MoveTarget, attacker.Pokemon, target.Pokemon );
            } else {
                var damageDetails = target.TakeDamage( move, attacker.Pokemon );
                yield return target.BattleHUD.UpdateHP();
                yield return ShowDamageDetails( damageDetails );
            }

            if( move.moveBase.SecondaryMoveEffects != null && move.moveBase.SecondaryMoveEffects.Count > 0 && target.Pokemon.currentHP > 0){
                foreach(var secondary in move.moveBase.SecondaryMoveEffects){
                    var rand = UnityEngine.Random.Range(1, 101);
                    if(rand <= secondary.Chance)
                        yield return RunMoveEffects(secondary, secondary.Target, attacker.Pokemon, target.Pokemon);
                }
            }
        } else {
            yield return _dialogueBox.TypeDialogue($"{attacker.Pokemon.PokeSO.pName}'s attack missed!");
        }

        attacker.Pokemon.OnAfterTurn();
        yield return attacker.BattleHUD.UpdateHP();
        if( moveEffects.SevereStatus != ConditionID.NONE )
            yield return CheckForFaintedCommand();
    }

    private bool CheckMoveAccuracy( MoveClass move, BattleUnit attacker, BattleUnit target ){
        if( move.moveBase.Alwayshits )
            return true;

        float moveAccuracy = move.moveBase.Accuracy;

        int accuracy = attacker.Pokemon.StatBoosts[ Stat.Accuracy ];
        int evasion = target.Pokemon.StatBoosts[ Stat.Evasion ];

        var modifierValue = new float[] { 1f, 4f / 3f, 5f / 3f, 2f, 7f / 3f, 8f / 3f, 3f };

        if( accuracy > 0 )
            moveAccuracy *= modifierValue[accuracy];
        else
            moveAccuracy /= modifierValue[-accuracy];

        if( evasion < 0 )
            moveAccuracy /= modifierValue[evasion];
        else
            moveAccuracy *= modifierValue[-evasion];

        return UnityEngine.Random.Range( 1, 101 ) <= moveAccuracy;
    }

    private IEnumerator RunMoveEffects( MoveEffects effects, MoveTarget moveTarget, PokemonClass attacker, PokemonClass target ){
        //--Modify Stats
        if(effects.StatBoostList != null){
            if( moveTarget == MoveTarget.self )
                attacker.ApplyStatBoost( effects.StatBoostList );
            else
                target.ApplyStatBoost( effects.StatBoostList );
        }

        //--Apply Severe Status Effects
        if( effects.SevereStatus != ConditionID.NONE ){
            target.SetSevereStatus( effects.SevereStatus );
        }

        //--Apply Volatile Status Effects
        if( effects.VolatileStatus != ConditionID.NONE ){
            target.SetVolatileStatus( effects.VolatileStatus );
        }

        yield return null;
    }

    private IEnumerator ShowDamageDetails( DamageDetails damageDetails ){
        //--critical hit dialogue
        if( damageDetails.Critical > 1 )
            yield return _dialogueBox.TypeDialogue( "It was a critical hit!" );

        //--super effective dialogue
        if( damageDetails.TypeEffectiveness > 1 )
            yield return _dialogueBox.TypeDialogue( "It's super effective!" );

        //--not very effective dialogue
        else if ( damageDetails.TypeEffectiveness < 1 )
            yield return _dialogueBox.TypeDialogue( "It wasn't very effective..." );
    }

    public IEnumerator CheckForFaintedCommand(){
        //--Check Player Fainted
        if( _playerUnit.Pokemon.currentHP <= 0 ){
            _isFainted = true;
            yield return new WaitForSeconds( 1f ); //--fainted animation placeholder

            yield return _dialogueBox.TypeDialogue( $"Your {_playerUnit.Pokemon.PokeSO.pName} fainted!" );

            var nextPokemon = _playerParty.GetHealthyPokemon();
            if( nextPokemon == null ){
                EndBattle();
            }

        }

        //--Check Enemy Fainted
        if( _enemyUnit.Pokemon.currentHP <= 0 ){
            _isFainted = true;
            if( _enemyUnit.Pokemon == _wildPokemon )
                yield return _dialogueBox.TypeDialogue( $"The wild {_enemyUnit.Pokemon.PokeSO.pName} fainted!" );
            else
                yield return _dialogueBox.TypeDialogue( $"The Enemy {_enemyUnit.Pokemon.PokeSO.pName} fainted!" );

            yield return new WaitForSeconds( 0.5f );
            EndBattle();
        }

        yield return null;
    }

    private void EndBattle(){
        Debug.Log( "endbattle called" );
        _isFainted = false;
        _commandQueue.Clear();
        OnBattleEnded?.Invoke();
        BattleUIActions.OnAttackPhaseCompleted?.Invoke();
        _battleStateEnum = BattleStateEnum.Over;
        PlayerReferences.AIPath.enabled = false;
        GameStateTemp.GameState = GameState.Overworld;
        GameStateTemp.OnGameStateChanged?.Invoke();
    }

    public IEnumerator PerformSwitchPokemonCommand( PokemonClass pokemon ){
        //--for the future if i want to target the previous pokemon with a move (pursuit), specificy the previous pokemon in this class?

        _battleStateEnum = BattleStateEnum.Busy;
        if( !_isFaintedSwitch ){
            yield return _dialogueBox.TypeDialogue( $"{_playerUnit.Pokemon.PokeSO.pName}, come back!" );
        }
        
        _playerUnit.Setup( pokemon, _playerHUD );
        _fightMenu.SetUpMoves( pokemon.Moves );

        yield return _dialogueBox.TypeDialogue( $"Go, {pokemon.PokeSO.pName}!" );
        yield return new WaitForSeconds(1f);
        // BattleUIActions.OnCommandAnimationsCompleted?.Invoke();

        if( _isFaintedSwitch )
            _isFaintedSwitch = false;
        yield return new WaitForSeconds(0.5f);
        PlayerAction();
    }

    public IEnumerator PerformRunFromBattleCommand(){
        Debug.Log( "You got away!" );
        EndBattle();
        yield return new WaitForSeconds(1f);
    }

//--------------------------------------------------------------------------------------------------------
//---------------------------------------COMMAND SYSTEM METHODS-------------------------------------------
//--------------------------------------------------------------------------------------------------------

    public void DetermineCommandOrder(){
        _commandList = _commandList.OrderBy( prio => prio.CommandPriority ).ThenBy( prio => prio.UnitAgility ).ToList();

        for( int i = _commandList.Count - 1; i >= 0; i-- ){
            AddCommand( _commandList[i] );
            _commandList.RemoveAt( i );
        }

        StartCoroutine( ExecuteCommandQueue() );
    }

    public void SetPlayerMoveCommand( BattleUnit attacker, MoveClass move ){
        _useMoveCommand = new UseMoveCommand( move, attacker, _enemyUnit, this );
        _commandList.Add( _useMoveCommand );
        OnPlayerCommandSelect?.Invoke();
    }

    public void SetEnemyMoveCommand( BattleUnit attacker, MoveClass move ){
        _useMoveCommand = new UseMoveCommand( move, attacker, _playerUnit, this );
        _commandList.Add( _useMoveCommand );
        DetermineCommandOrder();
    }

    public void SetSwitchPokemonCommand( PokemonClass pokemon ){
        _switchPokemonCommand = new SwitchPokemonCommand( pokemon, this );
        _commandList.Add( _switchPokemonCommand );
        OnPlayerCommandSelect?.Invoke();
    }

    public void SetRunFromBattleCommand(){
        _runFromBattleCommand = new RunFromBattleCommand( this );
        _commandList.Add( _runFromBattleCommand );
        OnPlayerCommandSelect?.Invoke();
    }

    public void AddCommand( IBattleCommand command ){
        _commandQueue.Enqueue( command );
    }

    public IEnumerator ExecuteCommandQueue(){
        while( _commandQueue.Count > 0 )
        {
            _battleStateEnum = BattleStateEnum.Busy;
            // yield return UpdateAllUnitsHP();
            yield return _commandQueue.Dequeue().ExecuteBattleCommand();
            _turnsLeft = _commandQueue.Count;

            yield return CheckForFaintedCommand();

            if( _isFainted ){
                //--in a 1v1, if a mon on either side faints, the commands are reset
                //--i will have to go about this much differently in the future i'm sure
                Debug.Log( "Fainted should be true: " + _isFainted );
                _commandQueue.Clear();
                break;
            }
        }

        if( _isFainted ){
            _isFainted = false;
            yield return new WaitForSeconds( 0.5f );
            OpenPartyMenu();
            yield return null;
        } else {
            BattleUIActions.OnAttackPhaseCompleted?.Invoke();
            yield return new WaitForSeconds( 0.5f );
            PlayerAction();
            yield return null;
        }

    }
    
}
