using System.Collections;
using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using Cinemachine;
using UnityEngine.EventSystems;
using NoxNoctisDev.StateMachine;

public enum BattleStateEnum { ActionSelect, Busy, SelectingNextPokemon }
public enum BattleFlag { SpeedChange, TrickRoom, Redirect, Uproar }

public enum BattleType { WildBattle_1v1, WildBattle_2v2, TrainerSingles, TrainerDoubles, TrainerMulti_2v1, TrainerMulti_2v2, AI_Singles, AI_Doubles, PvP_Singles, PvP_Doubles }

[Serializable]
public class BattleSystem : MonoBehaviour
{
    [Header( "States" )]
    public StateStackMachine<BattleSystem> StateMachine;
    [SerializeField] private BattleSystem_BusyState _busyState;    
    [SerializeField] private BattleSystem_ActionSelectState _actionSelectState;
    [SerializeField] private BattleSystem_AITurnState _aiTurnState;
    [SerializeField] private BattleSystem_RunCommandQueueState _runCommandQueueState;
    [SerializeField] private BattleSystem_ForceSelectPokemonState _forceSelectPokemonState;
    [SerializeField] private BattleSystem_RoundEndPhaseState _roundEndPhaseState;
    [SerializeField] private PostBattleSummary _postBattleSummary;
    public BattleSystem_BusyState BusyState => _busyState;
    public BattleSystem_ActionSelectState ActionSelectState => _actionSelectState;
    public BattleSystem_AITurnState AITurnState => _aiTurnState;
    public BattleSystem_RunCommandQueueState CommandQueueState => _runCommandQueueState;
    public BattleSystem_ForceSelectPokemonState ForceSelectPokemonState => _forceSelectPokemonState;
    public BattleSystem_RoundEndPhaseState RoundEndPhaseState => _roundEndPhaseState;

#region Private Serialized References
    //================================[ REFERENCES ]===========================================
    //--Serialized Fields/private-----------------------------------------
    [SerializeField] private BattleCommandCenter _commandCenter;
    [SerializeField] private BattleArena _battleArena;
    [SerializeField] private BattleComposer _battleComposer;
    [SerializeField] private Texture2D _statUpEffectTex;
    [SerializeField] private Texture2D _statDownEffectTex;
    [SerializeField] private EventSystem _eventSystem;
    [SerializeField] private List<BattleHUD> _playerHUDs;
    [SerializeField] private List<BattleHUD> _enemyTrainerHUDs;
    [SerializeField] private BattleHUD _wildPokemonHUD;
    [SerializeField] private GameObject _wildPokemonCanvas;    
    [SerializeField] private GameObject _topTrainerCanvas;
    [SerializeField] private GameObject _bottomTrainerCanvas;
    [SerializeField] private GameObject _playerMenus;
    [SerializeField] private AbilityCutIn _abilityCutIn;
    [SerializeField] private Transform _damageTakenPopupPrefab;
    [SerializeField] private PlayerBattleMenu _battleMenu;
    [SerializeField] private FightMenu _fightMenu;
    [SerializeField] private PartyScreen_Battle _pkmnMenu;
    [SerializeField] private PartyDisplay _partyDisplay;
    [SerializeField] private LearnMove_Battle _learnMoveMenu;
    [SerializeField] private GameObject _thrownPokeBall;
    [SerializeField] private CinemachineVirtualCamera _singleTargetCamera;
#endregion
#region Private References
    //--Private-----------------------------------------------------------
    private BattleType _battleType;
    private Dictionary<BattleFlag, bool> _battleFlags;
    private List<BattleUnit> _playerUnits;
    private List<BattleUnit> _enemyUnits;
    private Queue<Func<IEnumerator>> _eventQueue;
    private Queue<Func<IEnumerator>> _uiQueue;
#endregion
#region Public Getters and Properties
    //--public/getters/properties----------------------------------------------
    public static BattleSystem Instance;
    public BattleCommandCenter CommandCenter => _commandCenter;
    public Dictionary<BattleFlag, bool> BattleFlags => _battleFlags;
    public static bool BattleIsActive { get; private set; }
    public BattleComposer BattleComposer => _battleComposer;
    public EventSystem EventSystem => _eventSystem;
    public BattleType BattleType => _battleType;
    public BattleArena BattleArena => _battleArena;
    //--Units
    public BattleTrainer TopTrainer1 => _topTrainer1;
    public BattleTrainer TopTrainer2 => _topTrainer2;
    public BattleTrainer BottomTrainer1 => _bottomTrainer1;
    public BattleTrainer BottomTrainer2 => _bottomTrainer2;
    public GameObject TrainerCenter_Top1 { get; private set; }
    public GameObject TrainerCenter2_Top { get; private set; }
    public GameObject TrainerCenter_Bottom1 { get; private set; }
    public GameObject TrainerCenter2_Bottom { get; private set; }
    public List<BattleUnit> PlayerUnits => _playerUnits;
    public List<BattleUnit> EnemyUnits => _enemyUnits;
    public BattleUnit SwitchUnitToPosition;
    public Move LastUsedMove { get; private set; }
    //--HUD
    public List<BattleHUD> PlayerHUDs => _playerHUDs;
    public List<BattleHUD> EnemyHUDs => _enemyTrainerHUDs;
    public BattleHUD WildPokemonHUD => _wildPokemonHUD;
    //--Dialogue/Damage Text
    public AbilityCutIn AbilityCutIn => _abilityCutIn;
    public static Action<Func<IEnumerator>> EnqueueEventCoroutine;
    public static Action<Func<IEnumerator>> EnqueueUICoroutine;
    public static Transform DamageTakenPopupPrefab;
    //--Menus and Screens
    public PlayerBattleMenu PlayerBattleMenu => _battleMenu;
    public FightMenu FightMenu => _fightMenu;
    public PartyScreen_Battle PKMNMenu => _pkmnMenu;
    public PartyDisplay PartyDisplay => _partyDisplay;
    public LearnMove_Battle LearnMoveMenu => _learnMoveMenu;
    //--EXP
    public int TotalPartyExpGain { get; private set; }
    public int TotalPartyEffortGain { get; private set; }
#endregion
#region State Machine
//=============================[ STATE MACHINE ]===============================================================

    private BattleStateEnum _battleStateEnum;
    public BattleStateEnum BattleStateEnum => _battleStateEnum;
    public StateStackMachine<BattleSystem> BattleSystemStateMachine { get; private set; }
#endregion
#region  Events
//================================[ EVENTS ]===================================================================
    public static event Action OnBattleStarted;
    public static event Action OnBattleEnded;
    public static event Action OnCommandAdded;
    public static Action OnPlayerPokemonFainted;
    public static Action OnPlayerChoseNextPokemon;
    public static Action<List<Pokemon>> OnBattlePartyUpdated;
#endregion
//----------------------------------------------------------------------------
    public bool BattleOver { get; private set; }
    private bool _isForcedSwitch;
    private bool _isAIHardSwitch;
    private bool _eventQueueProcessing;
    private bool _uiQueueProcessing;

#region Pokemon and Pokemon Parties
//=========================[ POKEMON AND PLAYER/TRAINER PARTIES ]================================================
    //--private
    private BattleTrainer _topTrainer1, _topTrainer2, _bottomTrainer1, _bottomTrainer2;
    private Inventory _playerInventory;
    public int _unitInSelectionState = 0;
    private Pokemon _wildPokemon;
    private WildPokemon _encounteredPokemon; //--wild pokemon object that you ran into

    //--public/getters/properties
    public Inventory PlayerInventory => _playerInventory;
    public BattleUnit UnitInSelectionState => _playerUnits[_unitInSelectionState];
    public int ActivePlayerUnitsCount => _playerUnits.Count( u => u !=null && u.Pokemon != null && u.Pokemon.CurrentHP > 0 );
    public int ActiveSecondPlayerUnitsCount = 0;
    public int ActiveEnemyUnitsCount => _enemyUnits.Count( u => u !=null && u.Pokemon != null && u.Pokemon.CurrentHP > 0 );
    public Pokemon WildPokemon => _wildPokemon;
    public WildPokemon EncounteredPokemon => _encounteredPokemon;
    public bool IsForcedSwitch => _isForcedSwitch;
    public bool HandleFaintCompleted { get; private set; }
#endregion
//============================================================================================================
    public Battlefield Field { get; private set; }

#region Command System
//====================================[ COMMAND SYSTEM ]=======================================================

    public List<IBattleCommand> CommandList { get; private set; }
    public Queue<IBattleCommand> CommandQueue { get; private set; }
    private UseMoveCommand _useMoveCommand;
    private UseItemCommand _useItemCommand;
    private SwitchPokemonCommand _switchPokemonCommand;
    private RunFromBattleCommand _runFromBattleCommand;
#endregion

//============================================================================================================
//============================================================================================================
//============================================================================================================

    private void OnEnable(){
        Instance = this;
        StateMachine = new( this );
        CommandCenter.Setup( this );
        _playerInventory = PlayerReferences.Instance.PlayerInventory;

        _roundEndPhaseState.Init();
        MoveSuccessDB.Init();
        MoveConditionDB.Init();
        CourtConditionDB.Init();

        _playerUnits = new();
        _enemyUnits = new();
        CommandQueue = new Queue<IBattleCommand>();
        CommandList = new List<IBattleCommand>();
        Field = new( this );
        InitializeBattleFlags();

        //--UI Queue, currently encompasses dialogue/system messages & ability cut ins -- 12/01/25
        _eventQueue = new();
        _uiQueue = new();
        EnqueueEventCoroutine = ( cr ) => AddToEventQueue( cr );
        EnqueueUICoroutine = ( cr ) => AddToUIQueue( cr );
        StartCoroutine( EventQueueRunner() );
        StartCoroutine( UIQueueRunner() );

        TotalPartyExpGain = 0;
        TotalPartyEffortGain = 0;
        BattleIsActive = true;
        OnBattleStarted?.Invoke();
    }

    private void OnDisable(){
        _roundEndPhaseState.Clear();
        Instance = null;
    }

    //--Battle System State Functions. The Current BattleSystem doesn't necessarily progress through states
    //--In a way that is significant. Currently, the flow of the battle system is mostly hard-coded, and
    //--depending on the current point in the flow, the state will change between busy, playeraction, and selecting a pokemon
    //--if the user's pokemon has been fainted (or eventually if forced to switch by shit like self u-turn or enemy roar)
    //--therefore, based on the actual code executed for each particular state, i don't think it's necessary to manage a state machine
    //--the one upside of using a state stack machine is being able to literally stack and pop each "state" in any order we might need
    //--eliminating any manual management of the states beyond pushing or popping the current state. buut...
    //--we're literally ONLY managing the player's controls in each state. as long as we call the appropriate "state"
    //--function, the correct set of controls should be set at any given time.

    //--11/25/25, 2:08am, we're adding a god damn state stack machine lol. even though the current states are trivial, the
    //--turn manager existing as a state makes sense, and more importantly, for doubles we need slightly more fine-grained control
    //--over the player's action select state. this state will then allow the battle menu to enter a target select state, depending
    //--on if there's multiple enemy units. the action select state will persist until all player units have added an action to a temporary list
    //--and then it will push the turn manager state to the top of the stack, with the action select state being the state machine's "base state"

    public void PopState(){
        StateMachine.Pop();
    }

    public void ChangeState( State<BattleSystem> newState ){
        StateMachine.ChangeState( newState );
    }

    public void PushState( State<BattleSystem> newState ){
        StateMachine.Push( newState );
    }

    public void SetBusyState()
    {
        if( BattleType == BattleType.AI_Singles || BattleType == BattleType.AI_Doubles )
            return;

        if( PlayerBattleMenu.StateMachine.CurrentState != PlayerBattleMenu.PausedState )
            PlayerBattleMenu.OnPauseState?.Invoke();
    }

    public void SetStateEnum( BattleStateEnum state )
    {
        _battleStateEnum = state;
    }

    public void SetUnitInSelectionState( int i )
    {
        _unitInSelectionState = i;
    }

    public void HandleTwoTurnMoves( BattleUnit unit )
    {
        Debug.Log( $"[Charge] Handling Two Turn Moves!" );
        if( unit.Flags[UnitFlags.Charging].IsActive && unit.Flags[UnitFlags.Charging].Count > 0 )
        {
            var move = unit.Flags[UnitFlags.Charging].Move;
            List<BattleUnit> targets = new() { unit.Flags[UnitFlags.Charging].Target, };
            Debug.Log( $"[Charge] {unit.Pokemon.NickName} was charging {move.MoveSO.Name}! Adding it to the command list...!" );
            SetMoveCommand( unit, targets, move );
        }
        
        if( unit.Flags[UnitFlags.Recharging].IsActive )
        {
            OnCommandAdded?.Invoke();
        }
    }

    private void InitializeBattleFlags()
    {
        _battleFlags = new();

        foreach( BattleFlag flag in Enum.GetValues( typeof( BattleFlag ) ) )
        {
            _battleFlags[flag] = false;
        } 
    }

    public void SetCommandList( List<IBattleCommand> commandList )
    {
        CommandList = commandList;
    }

    private void ExitUnits()
    {
        List<BattleUnit> activeUnits = GetActivePokemon();

        foreach( var unit in activeUnits )
        {
            Field.Weather?.OnExitWeather?.Invoke( unit.Pokemon );
            unit.Pokemon.BattleItemEffect?.OnItemExit?.Invoke( unit );
        }
    }
    
    private void ClearCourts()
    {
        foreach( var court in Field.ActiveCourts )
        {
            foreach( var condition in court.Value.Conditions.Values )
            {
                condition?.OnEnd?.Invoke( this, Field );

                foreach( var unit in court.Value.Units )
                {
                    condition?.OnExitCourt?.Invoke( unit, Field );
                }
            }

            court.Value.Conditions.Clear();
        }

        Field.ActiveCourts.Clear();
    }

    public void SetBattleFlag( BattleFlag flag, bool value )
    {
        Debug.Log( $"Setting BattleFlag: {flag} to {value}" );
        if( _battleFlags.ContainsKey( flag ) )
            _battleFlags[flag] = value;
        else
            Debug.LogError( "Battleflag not found!" );
    }

    public void SetLastUsedMove( Move move )
    {
        LastUsedMove = move;
    }

    public BattleUnit HandleRedirection( BattleUnit attacker, BattleUnit target, Move move )
    {
        if( move.MoveTarget == MoveTarget.Self || move.MoveTarget == MoveTarget.Ally || move.MoveTarget == MoveTarget.AllySide || move.MoveTarget == MoveTarget.All || move.MoveTarget == MoveTarget.AllField )
            return target;

        Debug.Log( $"[Battle System] Redirection set! Handling Redirection..." );
        var opponents = GetOpposingUnits( attacker );
        BattleUnit newTarget = target;

        for( int i = 0; i < opponents.Count; i++ )
        {
            var opp = opponents[i];
            if( opp.Pokemon.TransientStatus?.ID == TransientConditionID.CenterOfAttention )
            {
                Debug.Log( $"[Battle System] Redirection target found! Setting new target to: {opp.Pokemon.NickName}" );
                newTarget = opp;
            }
            else
                continue;
        }

        return newTarget;
    }

    public bool IsImprisoned( Move move, BattleUnit attacker )
    {
        if( attacker.ImprisonedBy == null )
            return false;

        var imprisoner = attacker.ImprisonedBy;
        for( int i = 0; i < imprisoner.ActiveMoves.Count; i++ )
        {
            var m = imprisoner.ActiveMoves[i];
            if( move.MoveSO == m.MoveSO )
                return true;
            else
                continue;
        }

        return false;
    }

    public void EnablePokemonSelectScreen()
    {            
        if( PlayerBattleMenu.StateMachine.CurrentState == PlayerBattleMenu.PausedState )
            PlayerBattleMenu.OnChangeState?.Invoke( _pkmnMenu );
    }

    public void AddToEventQueue( Func<IEnumerator> cr, [System.Runtime.CompilerServices.CallerFilePath] string file = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0 )
    {
        _eventQueue.Enqueue( cr );
        Debug.Log( $"[Event Queue] {_eventQueue.Peek()} was Enqueued from {System.IO.Path.GetFileName( file )}:{line}" );
    }

    private IEnumerator EventQueueRunner()
    {
        while( true )
        {
            if( _eventQueue.Count > 0 )
            {
                _eventQueueProcessing = true;
                var next = _eventQueue.Dequeue();
                // Debug.Log( $"[Event Queue] Starting Event Coroutine: {next()}" );
                yield return next();
                _eventQueueProcessing = false;
                // Debug.Log( $"[Event Queue] Finished Event Coroutine: {next()}" );
            }
            else
            {
                yield return null;
            }

            // Debug.Log( $"[Event Queue] The Event Queue's Count > 0 is: {_eventQueue.Count}" );
        }
    }

    public IEnumerator WaitForEventQueue()
    {
        if( _eventQueue == null )
            yield break;
            
        yield return new WaitUntil( () => _eventQueue.Count == 0 && !_eventQueueProcessing );
        yield return WaitForUIQueue();
    }

    public void AddDialogue( string dialogue )
    {
        AddToEventQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( dialogue ) );
    }

    public void AddToUIQueue( Func<IEnumerator> cr, [System.Runtime.CompilerServices.CallerFilePath] string file = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0 )
    {
        _uiQueue.Enqueue( cr );
        Debug.Log( $"[UI Queue] {_uiQueue.Peek()} was Enqueued from {System.IO.Path.GetFileName( file )}:{line}" );
    }

    private IEnumerator UIQueueRunner()
    {
        while( true )
        {
            if( _uiQueue.Count > 0 )
            {
                _uiQueueProcessing = true;
                var next = _uiQueue.Dequeue();
                // Debug.Log( $"[UI Queue] Starting UI Coroutine: {next()}" );
                yield return next();
                // Debug.Log( $"[UI Queue] Waiting for Dialogue State to End." );
                yield return new WaitUntil( () => GameStateController.Instance.CurrentStateEnum != GameStateController.GameStateEnum.DialogueState );
                // Debug.Log( $"[UI Queue] Finished UI Coroutine: {next()}" );
                _uiQueueProcessing = false;
            }
            else
            {
                yield return null;
            }

            // Debug.Log( $"[UI Queue] The UI Queue's Count > 0 is: {_uiQueue.Count}" );
        }
    }

    public IEnumerator WaitForUIQueue()
    {
        if( _uiQueue == null )
            yield break;
            
        yield return new WaitUntil( () => _uiQueue.Count == 0 && !_uiQueueProcessing );
    }

    public IEnumerator CreateLifeStealEvent( BattleUnit drainedUnit, BattleUnit healedUnit, int stolenHP )
    {
        drainedUnit.Pokemon.DecreaseHP( stolenHP );
        AudioController.Instance.PlaySFX( SoundEffect.DamageEffective );
        yield return drainedUnit.PokeAnimator.PlayTakeDamageAnimation();
        yield return drainedUnit.BattleHUD.UpdateHPCoroutine();
        yield return drainedUnit.BattleHUD.WaitForHPUpdate();

        yield return new WaitForSeconds( 0.1f );

        healedUnit.Pokemon.IncreaseHP( stolenHP );
        AudioController.Instance.PlaySFX( SoundEffect.HPRestore );
        yield return healedUnit.PokeAnimator.PlayHealAnimation();
        yield return healedUnit.BattleHUD.UpdateHPCoroutine();
        yield return healedUnit.BattleHUD.WaitForHPUpdate();
        
        AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{drainedUnit.Pokemon.NickName} had its health drained by Leech Seed and given to {healedUnit.Pokemon.NickName}!") );
    }

    private List<Pokemon> GetCopyOfParty( List<Pokemon> party )
    {
        Debug.Log( $"Copying party!" );
        List<Pokemon> copyParty = new();

        for( int i = 0; i < party.Count; i++)
        {
            copyParty.Add( party[i] );
        }

        return copyParty;
    }

    //--Start setting up a battle. Anything that starts a battle needs to set the Battle Type. The Battle Type is responsible
    //--for HOW the Battle Stage will set itself up. From there, it will add all necessary unit positions to a list
    //--SOMEHOW, we will then assign all necessary Battle Unit objects from the Stage to their correct references here
    //--in the Battle System
    public void InitializeWildBattle( BattleTrainer player, BattleType battleType ){
        _battleType = battleType;

        //--Bottom Trainer (Player)
        _bottomTrainer1 = player;
        TrainerCenter_Bottom1 = PlayerReferences.Instance.PlayerCenter.gameObject;
        _playerMenus.SetActive( true );

        AudioController.Instance.PlayMusic( MusicTheme.BattleThemeDefault, 10f );

        Action activateCanvasesCallback = () =>
        {
            _wildPokemonCanvas.SetActive( true );
            _bottomTrainerCanvas.SetActive( true );
        };

        StartCoroutine( BattleSetup( activateCanvasesCallback ) );
    }

    public void InitializeTrainerSingles( BattleTrainer player, BattleTrainer cpu )
    {
        _battleType = BattleType.TrainerSingles;

        //--Top Trainer
        _topTrainer1 = cpu;
        TrainerCenter_Top1 = _topTrainer1.TrainerCenter;

        //--Bottom Trainer (Player)
        _bottomTrainer1 = player;
        TrainerCenter_Bottom1 = PlayerReferences.Instance.PlayerCenter.gameObject;
        _playerMenus.SetActive( true );

        AudioController.Instance.PlayMusic( cpu.BattleTheme, 10f );

        Action activateCanvasesCallback = () =>
        {
            _topTrainerCanvas.SetActive( true );
            _bottomTrainerCanvas.SetActive( true );
        };

        StartCoroutine( BattleSetup( activateCanvasesCallback ) );
    }

    public void InitializeTrainerDoubles( BattleTrainer player, BattleTrainer cpu )
    {
        _battleType = BattleType.TrainerDoubles;

        //-Top Trainer
        _topTrainer1 = cpu;
        TrainerCenter_Top1 = _topTrainer1.TrainerCenter;

        //--Enabling the second unit HUD for doubles
        for( int i = 0; i < _enemyTrainerHUDs.Count; i++ )
            _enemyTrainerHUDs[i].gameObject.SetActive( true );
        
        //--Bottom Trainer
        _bottomTrainer1 = player;
        TrainerCenter_Bottom1 = PlayerReferences.Instance.PlayerCenter.gameObject;
        _playerMenus.SetActive( true );

        //--Enabling the second unit HUD for doubles
        for( int i = 0; i < _playerHUDs.Count; i++ )
            _playerHUDs[i].gameObject.SetActive( true );

        AudioController.Instance.PlayMusic( cpu.BattleTheme, 10f );

        Action activateCanvasesCallback = () =>
        {
            _topTrainerCanvas.SetActive( true );
            _bottomTrainerCanvas.SetActive( true );
        };

        StartCoroutine( BattleSetup( activateCanvasesCallback ) );
    }

    public void InitializeAISingles( BattleTrainer topTrainer, BattleTrainer bottomTrainer )
    {
        _battleType = BattleType.AI_Singles;

        //--Top Court CPU Trainer
        _topTrainer1 = topTrainer;
        TrainerCenter_Top1 = _topTrainer1.TrainerCenter;

        //--Bottom Court CPU Trainer
        _bottomTrainer1 = bottomTrainer;
        TrainerCenter_Bottom1 = _bottomTrainer1.TrainerCenter;

        int coin = UnityEngine.Random.Range( 0, 2 );
        MusicTheme theme = coin == 0 ? topTrainer.BattleTheme : bottomTrainer.BattleTheme;
        AudioController.Instance.PlayMusic( theme, 5f );

        Action activateCanvasesCallback = () =>
        {
            _topTrainerCanvas.SetActive( true );
            _bottomTrainerCanvas.SetActive( true );
        };

        StartCoroutine( BattleSetup( activateCanvasesCallback ) );
    }

    private IEnumerator BattleSetup( Action canvasCallback )
    {
        _unitInSelectionState = 0;
        BattleUIActions.OnBattleSystemBusy?.Invoke();
        PushState( _busyState );
        yield return null;
        yield return BattleArena.PrepareArena( this );

        canvasCallback?.Invoke();

        yield return BeginBattle();
    }

    public void AssignUnits_1v1( BattleUnit playerUnit, BattleUnit enemyUnit ){
        _playerUnits.Add( playerUnit );
        _enemyUnits.Add( enemyUnit );
        Field.AddCourts( CourtLocation.TopCourt, _enemyUnits );
        Field.AddCourts( CourtLocation.BottomCourt, _playerUnits );
    }

    public void AssignUnits_2v2( List<BattleUnit> playerUnits, List<BattleUnit> enemyUnits )
    {
        _playerUnits = playerUnits;
        _enemyUnits = enemyUnits;
        Field.AddCourts( CourtLocation.TopCourt, _enemyUnits );
        Field.AddCourts( CourtLocation.BottomCourt, _playerUnits );
    }

    //--When a Wild Battle is triggered by WildPokemonEvents.OnPlayerEncounter (a player runs into a wild mon),
    //--the BattleController's InitWildBattle() is called. That method eventually passes the Encounter's reference
    //--to the BattleSystem so that BattleState_Setup can properly call Setup() on the EnemyUnit to properly add the
    //--Wild Encounter as the EnemyUnit's Pokemon
    public void AssignWildPokemon( WildPokemon wildPokemon ){
        _wildPokemon = wildPokemon.Pokemon;
        _encounteredPokemon = wildPokemon;
    }

    //--Need to make a round start phase!
    public IEnumerator BeginBattle()
    {
        Field.SetWeather( WeatherController.Instance.CurrentWeather );

        List<BattleUnit> activePokemon = new();
        activePokemon = GetActivePokemon();

        yield return new WaitUntil( () => !_battleArena.CMBrain.IsBlending );

        yield return null;
        //--Check for weather-setting abilites, and execute OnEnterWeather on every active pokemon per weather ability check.
        //--I can probably put the second loop inside the if check, but let's see if this works first --11/30/25

        //--Run all on enter abilities
        foreach( var unit in activePokemon )
        {
            unit.Pokemon.Ability?.OnAbilityEnter?.Invoke( unit.Pokemon, GetOpposingUnits( unit ), Field );

            unit.SetFlagActive( UnitFlags.ChoiceItem, false );
            unit.SetLastUsedMove( null );
            unit.Pokemon.BattleItemEffect?.OnItemEnter?.Invoke( unit );

            yield return null;

            foreach( var unitCheckStatusChange in activePokemon )
            {
                AddToEventQueue( () => ShowStatusChanges( unitCheckStatusChange ) );
                yield return null;
                yield return WaitForEventQueue();
            }
            
            yield return WaitForUIQueue();

            //--Turns taken in battle is defaulted to -1 so that we can simply increment a newly switched in pokemon at the very end of a round
            //--along side every other pokemon on the field. this would make a newly swapped in pokemon have taken 0 turns in battle
            //--for the first turn you can actually choose a command for them, which is correct.
            unit.ResetTurnsTakenInBattle(); //--Sets turns taken to -1
            unit.IncreaseTurnsTakenInBattle(); //--Since this is the beginning of a battle, the end of a round hasn't happened yet, so we increment here in its place, setting each turn taken to 0
            yield return new WaitForSeconds( 0.25f );
        }

        yield return new WaitForSeconds( 0.25f );

        if( BattleType == BattleType.AI_Singles || BattleType == BattleType.AI_Doubles )
            PushState( _aiTurnState );
        else
            PushState( _actionSelectState );
    }

    public void TriggerAbilityCutIn( Pokemon pokemon )
    {
        AddToUIQueue( () => _abilityCutIn.CutIn( pokemon, Field.GetUnitCourt( pokemon ).Location ) );
    }

    public void SetForcedSwitch( bool value )
    {
        _isForcedSwitch = value;
    }

    public void SetHandleFaintCompleted( bool value )
    {
        HandleFaintCompleted = value;
    }

    public void SetAIHardSwitch( bool value )
    {
        _isAIHardSwitch = value;
    }

    public bool IsPokemonSelectedToShift( Pokemon pokemon )
    {
        Debug.Log( "Checking if Pokemon is already selected" );
        //--We need to use the _commandList instead of the _commandQueue, because the queue doesn't get filled until all commands are added to the list
        //--The list then sorts itself appropriately, and feeds everything into the queue for the queue to run or dequeue accordingly.
        foreach( IBattleCommand command in CommandList )
        {
            Debug.Log( command.GetType() );
            if( command is SwitchPokemonCommand )
            {
                var switchCommand = command as SwitchPokemonCommand;
                Debug.Log( $"Command is a SwitchPokemonCommand to switch in: {switchCommand.GetPokemon().NickName}" );
                Debug.Log( $"The Selected Pokemon to shift is: {pokemon.NickName}" );
                if( switchCommand.GetPokemon() == pokemon )
                {
                    return true;
                }
            }
        }

        return false;
    }

    public List<BattleUnit> GetActivePokemon()
    {
        List<BattleUnit> activePokemon = new();

        for( int i = 0; i < _playerUnits.Count; i++ )
            activePokemon.Add( _playerUnits[i] );

        for( int i = 0; i < _enemyUnits.Count; i++ )
            activePokemon.Add( _enemyUnits[i] );

        if( _battleFlags[BattleFlag.TrickRoom] )
            activePokemon = activePokemon.OrderBy( u => u.Pokemon.Speed ).ToList();
        else
            activePokemon = activePokemon.OrderByDescending( u => u.Pokemon.Speed ).ToList();

        return activePokemon;
    }

    public BattleUnit GetPokemonBattleUnit( Pokemon pokemon )
    {
        var activePokemon = GetActivePokemon();
        BattleUnit unit = null;

        Debug.Log( $"[Get BattleUnit] Looking for {pokemon.NickName}'s ({pokemon.PID}) BattleUnit" );
        for( int i = 0; i < activePokemon.Count; i++ )
        {
            Debug.Log( $"[Get BattleUnit] Checking {activePokemon[i]}, pokemon is: {activePokemon[i].Pokemon.NickName} ({pokemon.PID})" );
            
            if( pokemon.PID == activePokemon[i].Pokemon.PID )
                unit = activePokemon[i];
        }

        Debug.Log( $"[Get BattleUnit] Get Battle Unit {unit.Pokemon.NickName} ({pokemon.PID})" );
        return unit;
    }

    public List<BattleUnit> GetAllyUnits( BattleUnit unit )
    {
        if( _playerUnits.Contains( unit ) )
            return _playerUnits;
        else
            return _enemyUnits;
    }

    public List<BattleUnit> GetOpposingUnits( BattleUnit unit )
    {
        if( _playerUnits.Contains( unit ) )
            return _enemyUnits;
        else
            return _playerUnits;
    }

    public List<Pokemon> GetAllyParty( BattleUnit unit )
    {
        if( TopTrainer1.Party.Contains( unit.Pokemon ) )
            return TopTrainer1.Party;
        else
            return BottomTrainer1.Party;
    }

    public List<Pokemon> GetOpposingParty( BattleUnit unit )
    {
        if( TopTrainer1.Party.Contains( unit.Pokemon ) )
            return BottomTrainer1.Party;
        else
            return TopTrainer1.Party;
    }

    //--Will call this where necessary in HandleFaintedPokemon()
    //--We won't be applying exp directly to pokemon immediately during battle. instead,
    //--The entire party will gain the combined EXP after battle is over. mons that participated
    //--at all will gain full exp, mons that did not will gain, for now, 75%
    //--I may actually need to place a method that actually applies the exp into the PokemonParty script
    //--since it will occur after battle. or, just have a "post battle" state before the battle system
    //--ends. that sounds more appropriate
    private IEnumerator HandleExpGain( BattleUnit faintedUnit )
    {
        if( BattleType == BattleType.AI_Singles || BattleType == BattleType.AI_Doubles )
            yield break;

        //--Exp Gain
        int expYield = faintedUnit.Pokemon.PokeSO.ExpYield;
        int unitLevel = faintedUnit.Pokemon.Level;
        float trainerBonus = ( BattleType == BattleType.TrainerSingles || BattleType == BattleType.TrainerDoubles ) ? 1.5f : 1f;

        int expGain = Mathf.FloorToInt( expYield * unitLevel * trainerBonus ) / 7;

        //--Effort Points Gain
        int effortYield = faintedUnit.Pokemon.PokeSO.EffortYield;

        //--Add to totals
        TotalPartyExpGain += expGain;
        TotalPartyEffortGain += effortYield;

        Debug.Log( $"[Battle System][Handle Exp Gain] {faintedUnit.Pokemon.NickName} has fainted! Exp: {TotalPartyExpGain}, EP: {TotalPartyEffortGain}" );

        yield return null;
    }

    public float GetTotalEffectiveness( Move move, BattleUnit target )
    {
        return TypeChart.GetEffectiveness( move.MoveType, target.Pokemon.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveType, target.Pokemon.PokeSO.Type2 );
    }

    private bool MoveTargetSelfSide( Move move )
    {
        if( move.MoveTarget == MoveTarget.Self )
            return true;

        if( move.MoveTarget == MoveTarget.Ally )
            return true;

        if( move.MoveTarget == MoveTarget.AllySide )
            return true;

        if( move.MoveTarget == MoveTarget.AllField )
            return true;

        return false;
    }

    public bool MoveSuccess( BattleUnit attacker, BattleUnit target, Move move, bool aiCheck = false )
    {
        //--This checks if the target of a move is currently protecting itself.
        if( !MoveTargetSelfSide( move ) )
        {
            if( target.Pokemon.TransientStatus?.ID == TransientConditionID.Protect && !move.MoveSO.Flags.Contains( MoveFlags.ProtectIgnore ) )
            {
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{target.Pokemon.NickName} protects itself!" ) );
                return false;
            }
        }
        
        if( GetTotalEffectiveness( move, target ) == 0 && move.MoveSO.MoveCategory != MoveCategory.Status )
        {
            if( !aiCheck )
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"It doesn't effect {target.Pokemon.NickName}..." ) );

            return false;
        }

        if( move.MoveType == PokemonType.Ground && target.Pokemon.Ability?.ID == AbilityID.Levitate )
        {
            if( !aiCheck )
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"It doesn't effect {target.Pokemon.NickName}..." ) );

            return false;
        }

        //--Taunt check. We should really convert the status lists to dictionaries or something...
        if( move.MoveSO.MoveCategory == MoveCategory.Status && attacker.Pokemon.VolatileStatuses.ContainsKey( VolatileConditionID.Taunt ) )
        {
            AddDialogue( $"{attacker.Pokemon.NickName} is taunted! It can't use {move.MoveSO.Name}!" );
            return false;
        }

        //--Priority Blocking
        if( move.Priority > MovePriority.Zero ) 
        {
            //--Quick Guard
            var opposingCourt = Field.GetOpposingCourtLocation( attacker );
            if( Field.ActiveCourts[opposingCourt].Conditions.ContainsKey( CourtConditionID.QuickGuard ) )
            {
                if( !aiCheck )
                    AddDialogue( $"{target.Pokemon.NickName} is protected by Quick Guard!" );

                return false;
            }

            //--Psychic Terrain
            if( !MoveTargetSelfSide( move ) && Field.Terrain?.ID == TerrainID.Psychic )
            {
                if( !aiCheck )
                    AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"But it's blocked by the Psychic terrain!" ) );

                return false;
            }
        }

        //--Semi Invulnerability of moves Phantom Force, Fly, Dig, and Dive.
        //--The AI can't ever process a move in the case of this currently (02/12/26), so we should skip this check for the ai altogether until i come up with
        //--an option for the ai to use here. in most cases, the ai should simply attack, or switch to a tank that can defend properly against the move (such as a flying type vs dig)
        if( !aiCheck )
        {
            if( target.Flags[UnitFlags.SemiInvulnerable].IsActive )
            {
                AddDialogue( $"But the move is unable to reach {target.Pokemon.NickName}!" );
                return false;
            }
        }

        if( move.MoveTarget == MoveTarget.Ally && ( BattleType == BattleType.AI_Singles || BattleType == BattleType.TrainerSingles || target.Pokemon.IsFainted() ) )
        {
            if( !aiCheck )
                AddDialogue( $"But the move failed!" );

            return false;
        }

        //--Move Success Database Check
        var key = move.MoveSO.Name;
        if( MoveSuccessDB.MoveSuccess.ContainsKey( key ) )
        {
            var moveSuccess = MoveSuccessDB.MoveSuccess[key];
            bool successful = moveSuccess.OnCheckSuccess( attacker, target, move, this );
            
            if( !aiCheck )
            {
                if( successful )
                {
                    if( moveSuccess.SuccessMessage != null )
                        AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( moveSuccess.SuccessMessage?.Invoke( attacker.Pokemon ) ) );
                }
                else
                {
                    if( moveSuccess.FailureMessage != null )
                        AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( moveSuccess.FailureMessage?.Invoke( attacker.Pokemon ) ) );
                }
            }

            return successful;
        }

        return true;
    }

    public IEnumerator ShowStatusChanges( BattleUnit unit ){
        Debug.Log( $"[Show Status Changes] Unit: {unit}, {unit.Pokemon.NickName}" );
        var pokemon = unit.Pokemon;

        if( pokemon.StatusChanges.Count == 0 )
            yield break;

        while( pokemon.StatusChanges.Count > 0 )
        {
            var statusEvent = pokemon.StatusChanges.Dequeue();

            if( statusEvent.Type == StatusEventType.StatChange )
            {
                Texture2D tex;
                if( statusEvent.StageChange > 0 )
                {
                    tex = _statUpEffectTex;
                    AudioController.Instance.PlaySFX( SoundEffect.StatUp );
                }
                else
                {
                    tex = _statDownEffectTex;
                    AudioController.Instance.PlaySFX( SoundEffect.StatDown );
                }

                Debug.Log( $"[Show Status Changes] Stat Change Event" );
                yield return unit.PokeAnimator.PlayStatChangeAnimation( tex, statusEvent.StageChange );
                Debug.Log( $"[Show Status Changes] Stat Change Event Complete" );
            }

            if( statusEvent.Type == StatusEventType.Damage )
            {
                Debug.Log( $"[Show Status Changes] Damage Event" );
                AudioController.Instance.PlaySFX( SoundEffect.DamageEffective );
                yield return unit.PokeAnimator.PlayTakeDamageAnimation();
                yield return unit.BattleHUD.UpdateHPCoroutine();
                yield return unit.BattleHUD.WaitForHPUpdate();
                Debug.Log( $"[Show Status Changes] Damage Event Complete" );
            }

            if( statusEvent.Type == StatusEventType.Heal )
            {
                Debug.Log( $"[Show Status Changes] Heal Event" );
                AudioController.Instance.PlaySFX( SoundEffect.HPRestore );
                yield return unit.PokeAnimator.PlayHealAnimation();
                yield return unit.BattleHUD.UpdateHPCoroutine();
                yield return unit.BattleHUD.WaitForHPUpdate();
                Debug.Log( $"[Show Status Changes] Heal Event Complete" );
            }

            if( statusEvent.Type == StatusEventType.SevereStatusDamage )
            {
                Debug.Log( $"[Show Status Changes] Severe Status Damage Event" );
                if( pokemon.SevereStatus != null && StatusIconAtlas.StatusIcons[pokemon.SevereStatus.ID].VFX != null )
                {
                    var vfxObj = Instantiate( StatusIconAtlas.StatusIcons[pokemon.SevereStatus.ID].VFX );
                    vfxObj.transform.SetPositionAndRotation( unit.PokeTransform.position, unit.PokeTransform.rotation );
                    Destroy( vfxObj, 2f );
                }

                AudioController.Instance.PlaySFX( SoundEffect.DamageEffective );
                yield return unit.PokeAnimator.PlayTakeDamageAnimation();
                yield return unit.BattleHUD.UpdateHPCoroutine();
                yield return unit.BattleHUD.WaitForHPUpdate();
                Debug.Log( $"[Show Status Changes] Severe Status Damage Event Complete" );
            }

            if( statusEvent.Type == StatusEventType.SevereStatusPassive )
            {
                Debug.Log( $"[Show Status Changes] Severe Status Passive Event" );
                if( pokemon.SevereStatus != null && StatusIconAtlas.StatusIcons[pokemon.SevereStatus.ID].VFX != null )
                {
                    var vfxObj = Instantiate( StatusIconAtlas.StatusIcons[pokemon.SevereStatus.ID].VFX );
                    vfxObj.transform.SetPositionAndRotation( unit.PokeTransform.position, unit.PokeTransform.rotation );
                    Destroy( vfxObj, 2f );
                }
                Debug.Log( $"[Show Status Changes] Severe Status Passive Event Complete" );
            }

            Debug.Log( $"[Show Status Changes] Checking if there's a message to add" );
            if( !string.IsNullOrEmpty( statusEvent.Message ) )
            {
                Debug.Log( $"[Show Status Changes] Message isn't null! Message: {statusEvent.Message}" );
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( statusEvent.Message ) );
            }

            yield return null;
            yield return WaitForUIQueue();
        }

    }

    //--Check to see if a pokemon is fainted
    //--I think i should...oh. I did add FNT as a status condition
    //--I should utilize that instead of adding a random bool to each pokemon.
    //--FNT should probably be applied immediately in the case that a pokemon's HP
    //--reahces <= 0. I need to then figure out a way to manage the Command Queue
    //--in case of a faint. In singles, all input commands are cleared after a pokemon
    //--faints. this works because the priority and status updates function in a way that
    //--unit switching and items will always go first, then moves will be run with
    //--respect to priority and unit speed, and then the board state will update
    //--with field effects or status effects ticking appropriately as the final thing
    //--CheckForFainted() should perhaps therefore be called in two places.
    //--Once after each move used by a pokemon currently out (total of 4 assuming doubles)
    //--or in the case of a move's recoil, or things like life orb, rough skin, other abilities, etc
    //--and then once after game board update in the case of statuses like BRN, FRST, PSN, TOX
    public IEnumerator CheckForFaint( BattleUnit checkUnit )
    {
        Debug.Log( $"[Move Command][UI Queue][Check For Faint] Checking for faint on {checkUnit.Pokemon.NickName}" );
        if( checkUnit.Pokemon.CurrentHP > 0 || checkUnit.Pokemon.SevereStatus?.ID == SevereConditionID.FNT )
            yield break; //--if the pokemon's hp is above 0 we simply leave, it hasn't fainted yet. If the pokemon has already fainted from an earlier phase check, we also leave

        checkUnit.Pokemon.CureSevereStatus(); //--Clear any potential Severe Status, which would prevent FNT from being assigned
        checkUnit.Pokemon.ClearAllVolatileStatus(); //--This also happens on faint, so it should be taken care of. Reminder to do so on switch too
        checkUnit.Pokemon.CureTransientStatus();
        checkUnit.Pokemon.CureBindingStatus();
        checkUnit.ResetTurnsTakenInBattle();
        checkUnit.SetFlagActive( UnitFlags.ChoiceItem, false );
        checkUnit.SetUnitTrapped( false );
        checkUnit.SetLastUsedMove( null );

        checkUnit.Pokemon.SetFainted(); //--Set fainted status condition
        yield return checkUnit.PokeAnimator.PlayFaintAnimation(); //--fainted animation placeholder

        checkUnit.SetFlagActive( UnitFlags.FaintedPreviousTurn, true );

        if( _bottomTrainer1.Party.Contains( checkUnit.Pokemon ) )
        {
            AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"Your {checkUnit.Pokemon.NickName} fainted!" ) );
            yield return WaitForUIQueue();
        }
        else
        {
            if( checkUnit.Pokemon == _wildPokemon )
            {
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"The wild {checkUnit.Pokemon.NickName} fainted!" ) );
                yield return WaitForUIQueue();
            }
            else if( BattleType == BattleType.TrainerSingles || BattleType == BattleType.TrainerDoubles || BattleType == BattleType.AI_Singles || BattleType == BattleType.AI_Doubles )
            {
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"The Enemy {checkUnit.Pokemon.NickName} fainted!" ) );
                yield return WaitForUIQueue();
            }
        }

        yield return null;
    }

    public bool CheckForBattleOver( List<Pokemon> activePokemon, Pokemon nextPokemon )
    {
        Debug.Log( $"[Battle System] Checking if the battle has ended..." );

        if( nextPokemon == null && activePokemon.Count == 0 )
        {
            Debug.Log( $"[Battle System] Battle is over!" );
            BattleOver = true;
            return true;
        }
        else
            return false;
    }

    //--Command Level
    public IEnumerator HandleFaintedPokemon( BattleUnit faintedUnit )
    {
        Debug.Log( $"[Battle System][Fainted Pokemon] {faintedUnit.Pokemon.NickName} has fainted!" );

        //--Remove the fainted unit's commands from the command queue, if it had any
        foreach( var command in CommandQueue )
        {
            if( command.User == faintedUnit )
                RemoveUnitCommandFromQueue( faintedUnit );
        }

        //--If Player Unit has fainted
        if( _playerUnits.Contains( faintedUnit ) )
        {
            //--For singles BattleTypes, we immediately clear the queue and let the player change pokemon
            //--if they have any more remaining in their party. if not, the battle ends, the player should be
            //--brought back to the last visited poke center
            var activePokemon = _playerUnits.Select( u => u.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
            var nextPokemon = BottomTrainer1.GetHealthyPokemon( dontInclude: activePokemon );

            if( CheckForBattleOver( activePokemon, nextPokemon ) )
            {
                yield return PostBattleScreen();
            }
            else if( nextPokemon == null && activePokemon.Count > 0 )
            {
                //--Continue the battle using the active pokemon. this is for double battles, where you lose an active mon and have no more left in your party to send out.
                // _playerUnits.Remove( faintedUnit );
                //--Create a function to set the fainted unit's hud to a nice fat pokeball X like in SV
                
            }
            else if( nextPokemon != null )
            {
                if( faintedUnit.IsAI )
                {
                    SetForcedSwitch( true );
                    var switchIn = faintedUnit.BattleAI.RequestedForcedSwitch();
                    yield return CommandCenter.PerformSwitchPokemonCommand( switchIn, faintedUnit, true );
                    yield return new WaitUntil( () => !_isForcedSwitch );
                }
                else
                {
                    yield return CommandCenter.ForcedSwitchPartyMenu( faintedUnit );
                    yield return new WaitUntil( () => !_isForcedSwitch );
                }
            }
        }
        else //--If Enemy Unit has fainted
        {
            //--For singles BattleTypes, we immediately clear the queue. In the case of an enemy trainer,
            //--we send out their next available pokemon, and if not, the battle is ended because the player won
            if( faintedUnit.Pokemon == _wildPokemon ){
                yield return HandleExpGain( faintedUnit );
                yield return PostBattleScreen();
            }
            else
            {
                //--Add exp gained from the fainted enemy mon to the exp gained pool. then we can go ahead and handle force-switching/ending the battle
                yield return HandleExpGain( faintedUnit );
                var activeEnemyPokemon = _enemyUnits.Select( u => u.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
                var remainingPokemon = TopTrainer1.GetHealthyPokemon( dontInclude: activeEnemyPokemon );

                if( CheckForBattleOver( activeEnemyPokemon, remainingPokemon ) )
                {
                    yield return PostBattleScreen();
                }
                else if( remainingPokemon == null && activeEnemyPokemon.Count > 0 )
                {
                    //--Continue the battle using the active pokemon. this is for double battles, where you lose an active mon and have no more left in your party to send out.
                    // _enemyUnits.Remove( faintedUnit );
                    //--Create a function to set the fainted unit's hud to a nice fat pokeball X like in SV
                }
                else if( remainingPokemon != null )
                {
                    SetForcedSwitch( true );
                    var switchIn = faintedUnit.BattleAI.RequestedForcedSwitch();
                    yield return CommandCenter.PerformSwitchPokemonCommand( switchIn, faintedUnit, true );
                    yield return new WaitUntil( () => !_isForcedSwitch );
                }
            }
        }

        yield return _runCommandQueueState.ReorderCommands();

        yield return new WaitUntil( () => !_isForcedSwitch );
        yield return null;
        SetHandleFaintCompleted( true );
    }

    public IEnumerator ThrowPokeball( Item ball ){
        var playerBallPosition = PlayerReferences.Instance.PlayerCenter.position;
        _playerInventory.UsePokeball( ball );

        PokeballItemSO ballItem = (PokeballItemSO)ball.ItemSO;
        PokeBallType ballType = ballItem.BallType;

        AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"You threw a {ball.ItemSO.ItemName}!" ) );
        yield return WaitForUIQueue();

        var thrownBall = Instantiate( _thrownPokeBall, playerBallPosition, Quaternion.identity );
        var originalPos = _enemyUnits[0].transform;
        Vector3 originalScale = _enemyUnits[0].transform.localScale;
        Vector3 ballBouncePos = new ( 0f, 0.5f, 0f );

        thrownBall.GetComponentInChildren<PokeballAnimator>().SetBallSprite( ball.ItemSO.Icon );

        AudioController.Instance.PlaySFX( SoundEffect.BattleBallThrow );
        var sequence = DOTween.Sequence();
        sequence.Append( thrownBall.transform.DOJump( _enemyUnits[0].transform.position, 3f, 1, 0.75f ) );
        sequence.Append( thrownBall.transform.DOJump( _enemyUnits[0].transform.position + ballBouncePos, 1f, 1, 0.75f ) );

        yield return sequence.WaitForCompletion();
        yield return _enemyUnits[0].PokeAnimator.PlayCaptureAnimation( thrownBall.transform );
        AudioController.Instance.PlaySFX( SoundEffect.BattleBallDrop );
        yield return thrownBall.transform.DOMoveY( originalPos.position.y, 0.5f ).WaitForCompletion();

        _singleTargetCamera.LookAt = thrownBall.transform;
        _singleTargetCamera.gameObject.SetActive( true );

        int shakeCount = TryToCatchPokemon( _enemyUnits[0].Pokemon, (PokeballItemSO)ball.ItemSO );
        // Debug.Log( _enemyUnit.PokeSO.CatchRate );
        Debug.Log( $"Shake Count: {shakeCount}" );

        for( int i = 0; i < Mathf.Min( shakeCount, 3 ); i++ ){
            yield return new WaitForSeconds( 0.5f );
            AudioController.Instance.PlaySFX( SoundEffect.BattleBallShake );
            yield return thrownBall.GetComponentInChildren<PokeballAnimator>().TryCaptureShake();
        }
        if( shakeCount == 4 ){
            //--Pokemon is Caught
            AudioController.Instance.PlaySFX( SoundEffect.BattleBallClick );
            yield return new WaitUntil( () => !AudioController.Instance.IsPlayingSFX );
            AudioController.Instance.PlaySFX( SoundEffect.CatchSuccess );
            AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{_enemyUnits[0].Pokemon.NickName} was caught!" ) );
            yield return WaitForUIQueue();
            yield return thrownBall.GetComponentInChildren<PokeballAnimator>().Fadeout( 1.5f, true );
            
            _singleTargetCamera.gameObject.SetActive( false );
            Destroy( thrownBall );
            yield return HandleExpGain( _enemyUnits[0] );
            //--Pokemon is added to party post battle, so it doesn't gain exp from itself
            yield return PostBattleScreen( _enemyUnits[0].Pokemon, ballType );
        }
        else{
            //--Pokemon eats your ass
            yield return new WaitForSeconds( 1f );
            yield return thrownBall.GetComponentInChildren<PokeballAnimator>().Fadeout( 0.25f, false );
            yield return _enemyUnits[0].PokeAnimator.PlayBreakoutAnimation( originalPos );
            _singleTargetCamera.gameObject.SetActive( false );

            Destroy( thrownBall );

            if( shakeCount == 0 )
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"It broke free!" ) );
            if( shakeCount == 1 )
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"Argh, almost caught!" ) );
            if( shakeCount == 2 )
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"Shoot, it was so close!" ) );
            if( shakeCount == 3 )
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"F U C K     ! ! !" ) );

            yield return WaitForUIQueue();
        }
    }

    private int TryToCatchPokemon( Pokemon pokemon, PokeballItemSO pokeball ){
        Debug.Log( $"Ball Catchrate: {pokeball.CatchRate}" );
        float a = ( 3 * pokemon.MaxHP - 2 * pokemon.CurrentHP ) * pokemon.PokeSO.CatchRate * pokeball.CatchRate * SevereConditionsDB.GetStatusBonus( pokemon.SevereStatus ) / ( 3 * pokemon.MaxHP );

        if( a >= 255 )
            return 4;

        float b = 1048560 / Mathf.Sqrt( Mathf.Sqrt( 16711680 / a) );

        int shakeCount = 0;

        while( shakeCount < 4 ){
            if( UnityEngine.Random.Range( 0, 65535 ) >= b )
                break;

            shakeCount++;
        }

        return shakeCount;
    }

    private IEnumerator PostBattleScreen( Pokemon caughtPokemon = null, PokeBallType ball = PokeBallType.PokeBall ){
        // _battleStateEnum = BattleStateEnum.Busy;
        // SetBusyState();
        WaitForSeconds wait = new( 0.5f );

        //--Add Caught Pokemon to Party here AFTER exp calculations, so it doesn't gain EXP from itself being caught LOL
        if( caughtPokemon != null){
            PlayerReferences.Instance.PlayerTrainer.AddPokemon( caughtPokemon, ball );
        }

        //--Post Trainer Battle
        if( BattleType == BattleType.TrainerSingles || BattleType == BattleType.TrainerDoubles ){
            _topTrainer1.OnDefeated?.Invoke();
        }

        if(  BattleType == BattleType.AI_Singles || BattleType == BattleType.AI_Doubles )
        {
            _topTrainer1.OnDefeated?.Invoke();
            _bottomTrainer1.OnDefeated?.Invoke();
        }

        yield return wait;
        EndBattle();
    }

    //--If the current Pokemon is the Active Pokemon, refresh the BattleHUD
    private IEnumerator RefreshHUD( Pokemon pokemon ){
        for( int i = 0; i <_playerUnits.Count; i++ )
        {
            if( pokemon == _playerUnits[i].Pokemon ){
                _playerUnits[i].BattleHUD.RefreshHUD();
                yield return _playerHUDs[i].SetExpSmooth( true );
            }
        }
    }

    private int _endBattleCount = 0;
    public void EndBattle(){
        Debug.Log( $"[Battle System][End Battle] End Battle Count: {_endBattleCount}" );
        if( _endBattleCount > 0 )
            return;

        _endBattleCount++;

        StopCoroutine( _runCommandQueueState.ExecuteCommandQueue() );
        StopCoroutine( WaitForUIQueue() );
        StopCoroutine( UIQueueRunner() );
        _uiQueue.Clear();
        
        ExitUnits(); //--Calls OnExit on active pokemon for weather and held items
        ClearCourts();

        if( BattleType == BattleType.AI_Singles || BattleType == BattleType.AI_Doubles )
        {
            foreach( var unit in _playerUnits )
            {
                unit.SetAI( false );
            }
        }

        if( BattleType == BattleType.WildBattle_1v1 ){
            _encounteredPokemon.Despawn();
            _encounteredPokemon = null;
        }

        _playerUnits.Clear();
        _enemyUnits.Clear();

        CommandQueue.Clear();
        CommandList.Clear();

        _battleArena.AfterBattleCleanup();
        _playerHUDs[1].gameObject.SetActive( false );
        _enemyTrainerHUDs[1].gameObject.SetActive( false );
        _wildPokemonCanvas.SetActive( false );
        _topTrainerCanvas.SetActive( false );
        _bottomTrainerCanvas.SetActive( false );
        _playerMenus.SetActive( false );

        Field.SetWeather( WeatherConditionID.None );
        
        OnBattleEnded?.Invoke();
        // PlayerReferences.Instance.PlayerTrainer.PostBattleSync( _bottomTrainer1 );

        BattleIsActive = false;
        BattleOver = false;
        GameStateController.Instance.GameStateMachine.Pop();
        AudioController.Instance.PlayMusic( AudioController.Instance.LastOverworldTheme, 5f );

        int exp = TotalPartyExpGain;
        int ep = TotalPartyEffortGain;
        TotalPartyExpGain = 0;
        TotalPartyEffortGain = 0;

        if( BattleType != BattleType.AI_Singles || BattleType != BattleType.AI_Doubles )
        {
            _postBattleSummary.gameObject.SetActive( true );
            _postBattleSummary.RunBattleSummary( exp, ep );
        }
    }

    private IEnumerator NextUnitInSelection()
    {
        PlayerBattleMenu.OnPauseState?.Invoke();
        //--Eventually we'll delay for things like animations and trigger other visual setups, like potentially showing the chosen action above the previous unit
        //--these things might as well be coroutines since there'll be tweening involved, so the delay can be built into the general duration of them
        yield return new WaitForSeconds( 0.5f );

        Debug.Log( $"[Move Command] Current unit index in selection: {_unitInSelectionState}" );

        if( ActivePlayerUnitsCount == _playerUnits.Count )
        {
            var prevUnit = UnitInSelectionState;
            _unitInSelectionState++;
                
            if( _unitInSelectionState > ActivePlayerUnitsCount - 1 )
                _unitInSelectionState = Mathf.Clamp( _unitInSelectionState, 0, ActivePlayerUnitsCount - 1 );
            else if( !_playerUnits[_unitInSelectionState].Flags[UnitFlags.Charging].IsActive )
                PlayerBattleMenu.OnUnpauseState?.Invoke();

            var currentUnit = UnitInSelectionState;

            //--In the case of doubles, the first mon in selection has their two turn moves checked and handled in ActionSelectState.
            //--HandleTwoTurnMoves ends up here in NextUnitSelection, where we increment the unit selection index. If that index is actually incremented
            //--We need to check to see if the new mon has to have a two turn move handled. Also, should there ever be three mons on the field, this flow
            //--should theoretically just continue infinitely until all unit selection indices have their two turn moves resolved.
            if( currentUnit.Pokemon.PID != prevUnit.Pokemon.PID )
                HandleTwoTurnMoves( _playerUnits[_unitInSelectionState] );
        }

        Debug.Log( $"[Move Command] Next unit index in selection: {_unitInSelectionState}" );

        yield return null;
    }

    public void BeginAIActionSelect()
    {
        if( StateMachine.CurrentState == _actionSelectState )
        {
            PopState();
            PushState( _aiTurnState );
        }
    }

    public void BeginCommandQueueState()
    {
        if( StateMachine.CurrentState == _aiTurnState )
            PopState();

        PushState( _runCommandQueueState );
    }

    private void RemoveUnitCommandFromQueue( BattleUnit unit )
    {
        List<IBattleCommand> newCommandList = new();

        foreach( var command in CommandQueue )
        {
            if( command.User != unit )
                newCommandList.Add( command );
        }

        CommandQueue.Clear();

        for( int i = 0; i < newCommandList.Count; i++ )
            AddCommand( newCommandList[i] );
    }

    public void SetMoveCommand( BattleUnit attacker, List<BattleUnit> targets, Move move, bool aiCommand = false )
    {
        _useMoveCommand = new UseMoveCommand( move, attacker, targets, this );
        CommandList.Add( _useMoveCommand );

        if( !aiCommand )
        {
            OnCommandAdded?.Invoke();
            StartCoroutine( NextUnitInSelection() );
        }
    }

    public void SetUseItemCommand( BattleUnit user, Pokemon pokemon, Item item ){
        _useItemCommand = new UseItemCommand( this, user, pokemon, item );
        CommandList.Add( _useItemCommand );
        OnCommandAdded?.Invoke();
        StartCoroutine( NextUnitInSelection() );
    }

    public void SetSwitchPokemonCommand( Pokemon pokemon, BattleUnit unitPosition, bool aiSwitch = false ){
        _switchPokemonCommand = new SwitchPokemonCommand( pokemon, this, unitPosition, aiSwitch );
        CommandList.Add( _switchPokemonCommand );

        if( !aiSwitch )
        {
            OnCommandAdded?.Invoke();
            StartCoroutine( NextUnitInSelection() );
        }
    }

    public void SetRunFromBattleCommand( BattleUnit user ){
        _runFromBattleCommand = new RunFromBattleCommand( this, user );
        CommandList.Add( _runFromBattleCommand );
        OnCommandAdded?.Invoke();
        StartCoroutine( NextUnitInSelection() );
    }

    public void AddCommand( IBattleCommand command ){
        CommandQueue.Enqueue( command );
    }

    #if UNITY_EDITOR
    private void OnGUI(){
        if( !StateMachineDisplays.Show_BattleSystemStateStack )
            return; 

        var style = new GUIStyle();
        style.font = Resources.Load<Font>( "Fonts/Gotham Bold Outlined" );
        style.fontSize = 30;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;
        style.richText = true;

        GUILayout.BeginArea( new Rect( 0, 500, 600, 500 ) );
        GUILayout.Label( "STATE STACK", style );
        foreach( var state in StateMachine.StateStack ){
            GUILayout.Label( state.GetType().ToString(), style );
        }
        GUILayout.EndArea();
    }
#endif

}
