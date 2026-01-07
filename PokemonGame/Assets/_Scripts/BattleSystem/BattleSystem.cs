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
public enum BattleFlag { SpeedChange, TrickRoom }

public enum BattleType { WildBattle_1v1, WildBattle_2v2, TrainerSingles, TrainerDoubles, TrainerMulti_2v1, TrainerMulti_2v2, AI_Singles, AI_Doubles }

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

#region Private Serialized References
    //================================[ REFERENCES ]===========================================
    //--Serialized Fields/private-----------------------------------------
    [SerializeField] private BattleCommandCenter _commandCenter;
    [SerializeField] private BattleArena _battleArena;
    [SerializeField] private BattleComposer _battleComposer;
    [SerializeField] private GameObject _battleUnitPrefab;
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
    public Trainer TopTrainer1 => _topTrainer1;
    public Trainer TopTrainer2 => _topTrainer2;
    public Trainer BottomTrainer1 => _bottomTrainer1;
    public Trainer BottomTrainer2 => _bottomTrainer2;
    public GameObject TrainerCenter_Top1 { get; private set; }
    public GameObject TrainerCenter2_Top { get; private set; }
    public GameObject TrainerCenter_Bottom1 { get; private set; }
    public GameObject TrainerCenter2_Bottom { get; private set; }
    public GameObject BattleUnitPrefab => _battleUnitPrefab;
    public List<BattleUnit> PlayerUnits => _playerUnits;
    public List<BattleUnit> EnemyUnits => _enemyUnits;
    public BattleUnit SwitchUnitToPosition;
    //--HUD
    public List<BattleHUD> PlayerHUDs => _playerHUDs;
    public List<BattleHUD> EnemyHUDs => _enemyTrainerHUDs;
    public BattleHUD WildPokemonHUD => _wildPokemonHUD;
    //--Dialogue/Damage Text
    public AbilityCutIn AbilityCutIn => _abilityCutIn;
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
#endregion
//----------------------------------------------------------------------------
    private bool _battleOver;
    private bool _isForcedSwitch;
    private bool _isAIHardSwitch;
    private bool _eventQueueEmpty;
    private bool _uiQueueEmpty;

#region Pokemon and Pokemon Parties
//=========================[ POKEMON AND PLAYER/TRAINER PARTIES ]================================================
    //--private
    [SerializeField] private Trainer _playerTrainer;
    private Trainer _topTrainer1, _topTrainer2, _bottomTrainer1, _bottomTrainer2;
    private Inventory _playerInventory;
    private int _unitInSelectionState = 0;
    private Pokemon _wildPokemon;
    private WildPokemon _encounteredPokemon; //--wild pokemon object that you ran into

    //--public/getters/properties
    public Inventory Inventory => _playerInventory;
    public PokemonParty BottomTrainerParty => _bottomTrainer1.TrainerParty;
    public PokemonParty TopTrainerParty => _topTrainer1.TrainerParty;
    public BattleUnit UnitInSelectionState => _playerUnits[_unitInSelectionState];
    public int ActivePlayerUnitsCount => _playerUnits.Count( u => u !=null && u.Pokemon != null && u.Pokemon.CurrentHP > 0 );
    public int ActiveEnemyUnitsCount => _enemyUnits.Count( u => u !=null && u.Pokemon != null && u.Pokemon.CurrentHP > 0 );
    public Pokemon WildPokemon => _wildPokemon;
    public WildPokemon EncounteredPokemon => _encounteredPokemon;
    public bool IsForcedSwitch => _isForcedSwitch;
#endregion
//============================================================================================================
    public Battlefield Field { get; private set; }

#region Command System
//====================================[ COMMAND SYSTEM ]=======================================================

    private List<IBattleCommand> _commandList;
    private Queue<IBattleCommand> _commandQueue;
    public Queue<IBattleCommand> CommandQueue => _commandQueue;
    private UseMoveCommand _useMoveCommand;
    private UseItemCommand _useItemCommand;
    private SwitchPokemonCommand _switchPokemonCommand;
    private RunFromBattleCommand _runFromBattleCommand;
#endregion

#region Round End Phase
    private BattleSystem_RoundEndPhaseState _roundEndPhase;
#endregion
//============================================================================================================
//============================================================================================================
//============================================================================================================

    private void OnEnable(){
        Instance = this;
        StateMachine = new( this );
        DamageTakenPopupPrefab = _damageTakenPopupPrefab;
        _playerInventory = PlayerReferences.Instance.PlayerInventory;

        _roundEndPhase = _roundEndPhaseState as BattleSystem_RoundEndPhaseState;
        _roundEndPhase.Init();

        _playerUnits = new();
        _enemyUnits = new();
        _commandQueue = new Queue<IBattleCommand>();
        _commandList = new List<IBattleCommand>();
        Field = new();
        Field.ActiveCourts = new();
        InitializeBattleFlags();

        //--UI Queue, currently encompasses dialogue/system messages & ability cut ins -- 12/01/25
        _uiQueue = new();
        EnqueueUICoroutine = ( cr ) => AddToUIQueue( cr );
        StartCoroutine( UIQueueRunner() );

        TotalPartyExpGain = 0;
        BattleIsActive = true;
        OnBattleStarted?.Invoke();
    }

    private void OnDisable(){
        _roundEndPhase.Clear();
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

    private void PushState( State<BattleSystem> newState ){
        StateMachine.Push( newState );
    }

    private void SetBusyState()
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

    private void InitializeBattleFlags()
    {
        _battleFlags = new();

        foreach( BattleFlag flag in Enum.GetValues( typeof( BattleFlag ) ) )
        {
            _battleFlags[flag] = false;
        } 
    }

    private void ClearBattleFlags()
    {
        _battleFlags.Clear();
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

    private void SetForceSelectPokemonState()
    {            
        if( PlayerBattleMenu.StateMachine.CurrentState == PlayerBattleMenu.PausedState )
            PlayerBattleMenu.OnChangeState?.Invoke( _pkmnMenu );
    }

    public void AddToEventQueue( Func<IEnumerator> cr, [System.Runtime.CompilerServices.CallerFilePath] string file = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0 )
    {
        _eventQueue.Enqueue( cr );
        Debug.Log( $"{_eventQueue.Peek()} was Enqueued from {System.IO.Path.GetFileName( file )}:{line}" );
    }

    private IEnumerator EventQueueRunner()
    {
        while( true )
        {
            if( _eventQueue.Count > 0 )
            {
                _eventQueueEmpty = false;
                var next = _eventQueue.Dequeue();
                Debug.Log( $"Starting Event Coroutine: {next()}" );
                yield return next();
                Debug.Log( $"Finished Event Coroutine: {next()}" );
            }
            else
            {
                yield return null;
                _eventQueueEmpty = true;
            }

            Debug.Log( $"The Event Queue's Count > 0 is: {_eventQueue.Count}" );
        }
    }

    private IEnumerator WaitForEventQueue()
    {
        if( _eventQueue == null )
            yield break;
            
        if( _uiQueue.Count > 0 )
            yield return new WaitUntil( () => _eventQueueEmpty );
        else
            yield return null;
    }

    public void AddToUIQueue( Func<IEnumerator> cr, [System.Runtime.CompilerServices.CallerFilePath] string file = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0 )
    {
        _uiQueue.Enqueue( cr );
        // Debug.Log( $"{_uiQueue.Peek()} was Enqueued from {System.IO.Path.GetFileName( file )}:{line}" );
    }

    private IEnumerator UIQueueRunner()
    {
        while( true )
        {
            if( _uiQueue.Count > 0 )
            {
                _uiQueueEmpty = false;
                var next = _uiQueue.Dequeue();
                // Debug.Log( $"Starting UI Coroutine: {next()}" );
                yield return next();
                yield return null;
                // Debug.Log( $"Finished UI Coroutine: {next()}" );
            }
            else
            {
                yield return null;
                _uiQueueEmpty = true;
            }

            yield return new WaitForSeconds( 0.1f );
            // Debug.Log( $"The UI Queue's Count > 0 is: {_uiQueue.Count}" );
        }
    }

    private IEnumerator WaitForUIQueue()
    {
        if( _uiQueue == null )
            yield break;
            
        if( _uiQueue.Count > 0 )
            yield return new WaitUntil( () => _uiQueueEmpty );
        else
            yield return null;
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
    public void InitializeWildBattle( BattleType battleType ){
        _battleType = battleType;

        //--Bottom Trainer (Player)
        _bottomTrainer1 = _playerTrainer;
        var copyParty = GetCopyOfParty( PlayerReferences.Instance.PlayerParty.Party );
        _bottomTrainer1.TrainerParty.GiveParty( copyParty );
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

    public void InitializeTrainerSingles( Trainer trainer )
    {
        _battleType = BattleType.TrainerSingles;

        //--Top Trainer
        _topTrainer1 = trainer;
        TrainerCenter_Top1 = _topTrainer1.TrainerCenter;

        //--Bottom Trainer (Player)
        _bottomTrainer1 = _playerTrainer;
        var copyParty = GetCopyOfParty( PlayerReferences.Instance.PlayerParty.Party );
        _bottomTrainer1.TrainerParty.GiveParty( copyParty );
        TrainerCenter_Bottom1 = PlayerReferences.Instance.PlayerCenter.gameObject;
        _playerMenus.SetActive( true );

        AudioController.Instance.PlayMusic( trainer.TrainerMusic, 10f );

        Action activateCanvasesCallback = () =>
        {
            _topTrainerCanvas.SetActive( true );
            _bottomTrainerCanvas.SetActive( true );
        };

        StartCoroutine( BattleSetup( activateCanvasesCallback ) );
    }

    public void InitializeTrainerDoubles( Trainer trainer )
    {
        _battleType = BattleType.TrainerDoubles;

        //-Top Trainer
        _topTrainer1 = trainer;
        TrainerCenter_Top1 = _topTrainer1.TrainerCenter;

        //--Enabling the second unit HUD for doubles
        for( int i = 0; i < _enemyTrainerHUDs.Count; i++ )
            _enemyTrainerHUDs[i].gameObject.SetActive( true );
        
        //--Bottom Trainer
        _bottomTrainer1 = _playerTrainer;
        var copyParty = GetCopyOfParty( PlayerReferences.Instance.PlayerParty.Party );
        _bottomTrainer1.TrainerParty.GiveParty( copyParty );
        TrainerCenter_Bottom1 = PlayerReferences.Instance.PlayerCenter.gameObject;
        _playerMenus.SetActive( true );

        //--Enabling the second unit HUD for doubles
        for( int i = 0; i < _playerHUDs.Count; i++ )
            _playerHUDs[i].gameObject.SetActive( true );

        AudioController.Instance.PlayMusic( trainer.TrainerMusic, 10f );

        Action activateCanvasesCallback = () =>
        {
            _topTrainerCanvas.SetActive( true );
            _bottomTrainerCanvas.SetActive( true );
        };

        StartCoroutine( BattleSetup( activateCanvasesCallback ) );
    }

    public void InitializeAISingles( Trainer topTrainer, Trainer bottomTrainer )
    {
        _battleType = BattleType.AI_Singles;

        //--Top Court CPU Trainer
        _topTrainer1 = topTrainer;
        TrainerCenter_Top1 = _topTrainer1.TrainerCenter;

        //--Bottom Court CPU Trainer
        _bottomTrainer1 = bottomTrainer;
        TrainerCenter_Bottom1 = _bottomTrainer1.TrainerCenter;

        AudioController.Instance.PlayMusic( topTrainer.TrainerMusic, 10f );

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
        _wildPokemon.SetAsEnemyUnit();
    }

    //--Need to make a round start phase!
    public IEnumerator BeginBattle()
    {
        Field.SetWeather( WeatherController.Instance.CurrentWeather );

        List<BattleUnit> activePokemon = new();
        activePokemon = GetActivePokemon();

        yield return new WaitUntil( () => !_battleArena.CMBrain.IsBlending );

        WeatherConditionID currentWeather = WeatherConditionID.NONE;
        if( Field.Weather != null )
        {
            if( Field.Weather?.StartMessage != null )
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( Field.Weather?.StartMessage ) );

            yield return null;
            yield return WaitForUIQueue();
            currentWeather = Field.Weather.ID;
        }

        yield return null;
        //--Check for weather-setting abilites, and execute OnEnterWeather on every active pokemon per weather ability check.
        //--I can probably put the second loop inside the if check, but let's see if this works first --11/30/25

        //--Run all on enter abilities
        foreach( var unit in activePokemon )
        {
            Debug.Log( $"Current Weather is: {currentWeather}" );
            if( unit.Pokemon.IsPlayerUnit )
                unit.Pokemon.Ability?.OnAbilityEnter?.Invoke( unit.Pokemon, _enemyUnits, Field );
            else
                unit.Pokemon.Ability?.OnAbilityEnter?.Invoke( unit.Pokemon, _playerUnits, Field );

            unit.SetFlagActive( UnitFlags.ChoiceItem, false );
            unit.SetLastUsedMove( null );
            unit.Pokemon.BattleItemEffect?.OnItemEnter?.Invoke( unit );

            yield return null;

            foreach( var unitCheckStatusChange in activePokemon )
            {
                yield return ShowStatusChanges( unitCheckStatusChange );
                yield return null;
            }
            
            yield return WaitForUIQueue();

            if( Field.Weather != null && currentWeather != Field.Weather?.ID )
            {
                if( Field.Weather?.StartByMoveMessage != null )
                    AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( Field.Weather?.StartByMoveMessage ) );

                yield return null;
                yield return WaitForUIQueue();
                currentWeather = Field.Weather.ID;
            }

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

    public void SetAIHardSwitch( bool value )
    {
        _isAIHardSwitch = value;
    }

    private IEnumerator ForcedSwitchPartyMenu( BattleUnit unitPosition ){
        SetForcedSwitch( true );
        SwitchUnitToPosition = unitPosition;
        SetForceSelectPokemonState();
        BattleUIActions.OnSubMenuOpened?.Invoke();
        BattleUIActions.OnPkmnMenuOpened?.Invoke();
        yield return new WaitUntil( () => !_isForcedSwitch );
        yield return new WaitForSeconds( 0.1f );
        yield return null;
    }
    
    //--Gunna need to make a "Forced Switch" Pokemon state for fainted unit and move-forced switching resolutions.
    //--It will likely help handle if the ai and the player both faint at the same time as well, right now it's somewhat of a problem
    public void SetForcedSwitchMon( Pokemon incomingMon, BattleUnit unitPosition ){
        StartCoroutine( PerformSwitchPokemonCommand( incomingMon, unitPosition ) );
    }

    public bool IsPokemonSelectedToShift( Pokemon pokemon )
    {
        Debug.Log( "Checking if Pokemon is already selected" );
        //--We need to use the _commandList instead of the _commandQueue, because the queue doesn't get filled until all commands are added to the list
        //--The list then sorts itself appropriately, and feeds everything into the queue for the queue to run or dequeue accordingly.
        foreach( IBattleCommand command in _commandList )
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

    private List<BattleUnit> GetActivePokemon()
    {
        List<BattleUnit> activePokemon = new();

        for( int i = 0; i < _playerUnits.Count; i++ )
            activePokemon.Add( _playerUnits[i] );

        for( int i = 0; i < _enemyUnits.Count; i++ )
            activePokemon.Add( _enemyUnits[i] );

        activePokemon = activePokemon.OrderByDescending( u => u.Pokemon.Speed ).ToList();

        return activePokemon;
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

    public PokemonParty GetAllyParty( BattleUnit unit )
    {
        if( TopTrainer1.TrainerParty.Party.Contains( unit.Pokemon ) )
            return TopTrainerParty;
        else
            return BottomTrainerParty;
    }

    public PokemonParty GetOpposingParty( BattleUnit unit )
    {
        if( TopTrainer1.TrainerParty.Party.Contains( unit.Pokemon ) )
            return BottomTrainerParty;
        else
            return TopTrainerParty;
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

    private IEnumerator PostBattleScreen( Pokemon caughtPokemon = null, PokeBallType ball = PokeBallType.PokeBall ){
        // _battleStateEnum = BattleStateEnum.Busy;
        // SetBusyState();
        WaitForSeconds wait = new( 0.5f );

        //--Add Caught Pokemon to Party here AFTER exp calculations, so it doesn't gain EXP from itself being caught LOL
        if( caughtPokemon != null){
            PlayerReferences.Instance.PlayerParty.AddPokemon( caughtPokemon, ball );
        }

        //--Post Trainer Battle
        if( BattleType == BattleType.TrainerSingles || BattleType == BattleType.TrainerDoubles ){
            _topTrainer1.SetDefeated();
        }

        if(  BattleType == BattleType.AI_Singles || BattleType == BattleType.AI_Doubles )
        {
            _topTrainer1.SetDefeated();
            _bottomTrainer1.SetDefeated();
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

    private void EndBattle(){
        StopCoroutine( ExecuteCommandQueue() );
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

        _commandQueue.Clear();
        _commandList.Clear();

        _battleArena.AfterBattleCleanup();
        _playerHUDs[1].gameObject.SetActive( false );
        _enemyTrainerHUDs[1].gameObject.SetActive( false );
        _wildPokemonCanvas.SetActive( false );
        _topTrainerCanvas.SetActive( false );
        _bottomTrainerCanvas.SetActive( false );
        _playerMenus.SetActive( false );

        Field.SetWeather( WeatherConditionID.NONE );
        
        OnBattleEnded?.Invoke();
        PlayerReferences.Instance.PlayerParty.UpdateParty();

        // ClearBattleFlags();

        BattleIsActive = false;
        _battleOver = false;
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

//-------------------------------------------------------------------------------------------------------
//--------------------------------------------[ COMMANDS ]-----------------------------------------------
//-------------------------------------------------------------------------------------------------------

    //--Perform any Move
    public IEnumerator PerformMoveCommand( Move move, BattleUnit attacker, BattleUnit target ){
        //--Assign last used move.
        attacker.SetLastUsedMove( move );

        if( move.MoveSO.Name != "Protect" )
            attacker.Flags[UnitFlags.SuccessiveProtectUses].Count = 0;

        // Debug.Log( $"{attacker.Pokemon.NickName} has used Protect: {attacker.Flags[UnitFlags.SuccessiveProtectUses].Count} times!" );

        //--Checks if there's a status impeding the pokemon from using a move this turn, such as sleep, flinch, first turn para, confusion, etc.
        bool canAttack = attacker.Pokemon.OnBeforeTurn();
        if( !canAttack ){
            yield return ShowStatusChanges( attacker ); //--This is the type of thing that gets added to the Event Queue
            yield return WaitForUIQueue();
            yield return attacker.BattleHUD.WaitForHPUpdate(); //--This is the type of thing that gets added to the Event Queue
            yield break;
        }

        yield return ShowStatusChanges( attacker );
        yield return WaitForUIQueue();

        move.PP--; //--Reduces the move's PP by 1

        AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{attacker.Pokemon.NickName} used {move.MoveSO.Name}!" ) );
        yield return WaitForUIQueue();

        //--Check if move is a multi-hit, and return the amount of hits rolled
        int hitAmount = move.MoveSO.GetHitAmount();
        int hits = 1;
        float typeEffectiveness = 1f;
        var checkMoveAccuracy = CheckMoveAccuracy( move, attacker, target );
        var damageDetails = new DamageDetails();

        if( MoveSuccess( attacker, target, move ) )
        {
            if( checkMoveAccuracy )
            {
                yield return _battleComposer.RunMoveToAttackPosition( move, attacker, target );
                yield return new WaitForSeconds( 0.25f );
                
                for( int i = 1; i <= hitAmount; i++ )
                {
                    if( move.MoveSO.MoveCategory == MoveCategory.Status )
                    {
                        //--Look at all of this shit just for magic bounce lol. I can probably reuse it for Mirror Armor n shit. --12/21/25
                        var newTarget = target;
                        if( move.MoveSO.Flags.Contains( MoveFlags.Reflectable ) && target.Pokemon.Ability?.Name == "Magic Bounce" )
                        {
                            newTarget = attacker;
                            TriggerAbilityCutIn( target.Pokemon );
                            AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{target.Pokemon.NickName}'s Magic Bounce reflected it back!" ) );
                            yield return WaitForUIQueue();
                        }

                        yield return _battleComposer.RunStatusAttackScene( move, attacker, target );
                        yield return RunMoveEffects( move, move.MoveSO.MoveEffects, move.MoveSO.MoveTarget, attacker, newTarget );
                    }
                    else
                    {
                        yield return _battleComposer.RunAttackAnimation( move, attacker, target );
                        
                        attacker.SetFlagActive( UnitFlags.DidDamage, true );
                        damageDetails = target.TakeDamage( move, attacker, Field.Weather );
                        typeEffectiveness = damageDetails.TypeEffectiveness;

                        if( typeEffectiveness != 0 )
                            yield return _battleComposer.RunTakeDamagePhase( typeEffectiveness, target );

                        yield return target.BattleHUD.WaitForHPUpdate();
                        yield return ShowDamageDetails( damageDetails );
                        yield return null;
                        yield return RunMoveEffects( move, move.MoveSO.MoveEffects, move.MoveSO.MoveTarget, attacker, target );
                    }

                    if( move.MoveSO.SecondaryMoveEffects != null && move.MoveSO.SecondaryMoveEffects.Count > 0 && target.Pokemon.CurrentHP > 0 )
                    {
                        foreach( var secondary in move.MoveSO.SecondaryMoveEffects ){
                            var rand = UnityEngine.Random.Range( 1, 101 );
                            float chanceModifier = attacker.Pokemon.Ability?.OnSecondaryEffectChanceModify?.Invoke() ?? 1f;
                            float chance = secondary.Chance * chanceModifier;
                            if( rand <= secondary.Chance )
                                yield return RunMoveEffects( move, secondary, secondary.Target, attacker, target );
                        }
                    }

                    hits = i;
                    if( target.Pokemon.CurrentHP <= 0 )
                        break;

                    target.Pokemon.BattleItemEffect?.OnAfterTakeDamage?.Invoke( target );
                    target.Pokemon.BattleItemEffect?.OnMoveContact?.Invoke( attacker, target, move );
                    target.Pokemon.Ability?.OnMoveContact?.Invoke( attacker, target, move ); //--Does the target have an ability that activates on the attacker's move making contact?
                    // attacker.Pokemon.Ability?.OnMoveContact?.Invoke( attacker, target, move ); //--Does the attacker have an ability that activates on its move making contact? //--Not sure how to manage this vs the above
                }

                yield return attacker.PokeAnimator.PlayReturnToDefaultPosition();

                yield return ShowTypeEffectiveness( typeEffectiveness );

                if( hitAmount > 1 )
                    AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"The Pokemon was hit {hits} times!" ) );
                    yield return WaitForUIQueue();
            }
            else
            {
                damageDetails = null;
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{attacker.Pokemon.NickName}'s attack missed!" ) );
                yield return WaitForUIQueue();
            }
        }
        else
        {
            yield return WaitForUIQueue();
        }

        yield return RunAfterMove( damageDetails, move.MoveSO, attacker, target );

        //--Check for faint after a move is used on the target
        yield return CheckForFaint( target );
    }

    public float GetTotalEffectiveness( Move move, BattleUnit target )
    {
        return TypeChart.GetEffectiveness( move.MoveType, target.Pokemon.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveType, target.Pokemon.PokeSO.Type2 );
    }

    public bool MoveSuccess( BattleUnit attacker, BattleUnit target, Move move, bool aiCheck = false )
    {
        if( GetTotalEffectiveness( move, target ) == 0 )
        {
            if( !aiCheck )
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"It doesn't effect {target.Pokemon.NickName}..." ) );

            return false;
        }

        if( move.MoveSO.Name == "Fake Out" )
        {
            Debug.Log( $"Fake Out user {attacker.Pokemon.NickName}'s Turn Count: {attacker.Flags[UnitFlags.TurnsTaken].Count}" );
            if( attacker.Flags[UnitFlags.TurnsTaken].Count > 0 )
            {
                return ReturnMoveFailed( aiCheck );
            }
        }

        if( move.MoveSO.Name == "Sunny Day" )
        {
            if( Field.Weather?.ID == WeatherConditionID.SUNNY )
            {
                return ReturnMoveFailed( aiCheck );
            }
        }

        if( move.MoveSO.Name == "Rain Dance" )
        {
            if( Field.Weather?.ID == WeatherConditionID.RAIN )
            {
                return ReturnMoveFailed( aiCheck );
            }
        }

        if( move.MoveSO.Name == "Sandstorm" )
        {
            if( Field.Weather?.ID == WeatherConditionID.SANDSTORM )
            {
                return ReturnMoveFailed( aiCheck );
            }
        }

        if( move.MoveSO.Name == "Snowscape" )
        {
            if( Field.Weather?.ID == WeatherConditionID.SNOW )
            {
                return ReturnMoveFailed( aiCheck );
            }
        }

        if( move.MoveSO.Name == "Tailwind" )
        {
            var court = Field.GetUnitCourt( attacker );
            if( court.Conditions.ContainsKey( CourtConditionID.Tailwind ) )
            {
                return ReturnMoveFailed( aiCheck );
            }
        }

        if( move.MoveSO.Name == "Reflect" )
        {
            var court = Field.GetUnitCourt( attacker );
            if( court.Conditions.ContainsKey( CourtConditionID.Reflect ) )
            {
                return ReturnMoveFailed( aiCheck );
            }
        }

        if( move.MoveSO.Name == "Light Screen" )
        {
            var court = Field.GetUnitCourt( attacker );
            if( court.Conditions.ContainsKey( CourtConditionID.LightScreen ) )
            {
                return ReturnMoveFailed( aiCheck );
            }
        }

        //--This checks if the target of a move is currently protecting itself.
        if( target.Pokemon.TransientStatusActive && target.Pokemon.TransientStatus != null )
        {
            if( target.Pokemon.TransientStatus.ID == StatusConditionID.Protect && !move.MoveSO.Flags.Contains( MoveFlags.ProtectIgnore ) )
            {
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{target.Pokemon.NickName} protects itself!" ) );
                return false;
            }
        }
        
        //--This checks if the user fails or succeeds at USING Protect, based on Protect's successive use failure rate.
        if( move.MoveSO.Name == "Protect" && !aiCheck )
        {
            int uses = attacker.Flags[UnitFlags.SuccessiveProtectUses].Count;
            float successChance = Mathf.Pow( 1f / 3f, uses );
            bool success;
            // Debug.Log( $"{attacker.Pokemon.NickName}'s Protect success chance is: {successChance}" );
            if( uses == 0 )
            {
                attacker.Flags[UnitFlags.SuccessiveProtectUses].Count++;
                return true;
            }
            else if( uses > 0 )
            {
                success = UnityEngine.Random.value <= successChance;

                if( success )
                {
                    AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{attacker.Pokemon.NickName} protects itself!" ) );
                    attacker.Flags[UnitFlags.SuccessiveProtectUses].Count++;
                    return true;
                }
                else
                {
                    attacker.Flags[UnitFlags.SuccessiveProtectUses].Count = 0;
                    return ReturnMoveFailed();
                }
            }
        }

        if( move.MoveSO.Flags.Contains( MoveFlags.Powder ) && target.Pokemon.CheckTypes( PokemonType.Grass ) )
        {
            return ReturnMoveFailed( aiCheck );
        }

        return true;
    }

    private bool ReturnMoveFailed( bool aiCheck = false )
    {
        if( !aiCheck )
            AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"But the move failed!" ) );
            
        return false;
    }

    private IEnumerator RunAfterMove( DamageDetails details, MoveSO move, BattleUnit attacker, BattleUnit target ){
        if( details == null )
            yield break;

        if( move.DrainPercentage != 0 ){
            int healedHP = Mathf.Clamp( Mathf.CeilToInt( details.DamageDealt / 100f * move.DrainPercentage ), 1, attacker.Pokemon.MaxHP );
            attacker.Pokemon.IncreaseHP( healedHP );

            yield return attacker.BattleHUD.WaitForHPUpdate();

            if( target.Pokemon == WildPokemon )
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"The wild {target.Pokemon.NickName} had its energy drained!" ) );
            else
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"The enemy {target.Pokemon.NickName} had its energy drained!" ) );

            yield return WaitForUIQueue();
        }

        if( move.Recoil.RecoilType != RecoilType.none ){
            int damage = 0;

            switch( move.Recoil.RecoilType )
            {
                case RecoilType.RecoilByMaxHP:
                    int maxHP = attacker.Pokemon.MaxHP;
                    damage = Mathf.FloorToInt( maxHP * ( move.Recoil.RecoilDamage / 100f ) );
                    attacker.TakeRecoilDamage( damage );
                break;

                case RecoilType.RecoilByDamage:
                    damage = Mathf.FloorToInt( details.DamageDealt * ( move.Recoil.RecoilDamage / 100f ) );
                    attacker.TakeRecoilDamage( damage );
                break;

                case RecoilType.RecoilByCurrentHP:
                    int currentHP = attacker.Pokemon.CurrentHP;
                    damage = Mathf.FloorToInt( currentHP * ( move.Recoil.RecoilDamage / 100f ) );
                    attacker.TakeRecoilDamage( damage );
                break;

                default:
                    Debug.LogError( "Unknown Recoil Effect!!" );
                break;
            }
        }

        yield return ShowStatusChanges( attacker );
        yield return WaitForUIQueue();

        yield return null;

        yield return ShowStatusChanges( target );
        yield return WaitForUIQueue();
    }

    private IEnumerator AfterTurnUpdate( BattleUnit user )
    {
        user.Pokemon.BattleItemEffect?.OnItemAfterTurn?.Invoke( user );
        yield return ShowStatusChanges( user );
        yield return null;

        yield return CheckForFaint( user );
        yield return null;

        yield return WaitForUIQueue();
        yield return null;

        if( user != null && user.Pokemon.IsFainted() )
            yield return HandleFaintedPokemon( user );

        yield return WaitForUIQueue();
        yield return null;

        user.Pokemon.Ability?.OnAbilityAfterTurn?.Invoke( user, Field );
        yield return ShowStatusChanges( user );
        yield return null;

        yield return CheckForFaint( user );
        yield return null;

        yield return WaitForUIQueue();
        yield return null;

        if( user != null && user.Pokemon.IsFainted() )
            yield return HandleFaintedPokemon( user );

        yield return WaitForUIQueue();
        yield return null;

        var activePokemon = GetActivePokemon();

        foreach( var unit in activePokemon )
        {
            yield return ShowStatusChanges( unit );
            yield return null;
            yield return WaitForUIQueue();
        }

        yield return null;
    }

    //--Will eventually be adjusted, and expanded to include item, weather, field effect, and other necessary post-turn ticks
    //--MAKE A TURN MANAGER INSTEAD HE HE !
    private IEnumerator RoundEndUpdate(){
        // if( _battleOver )
            // yield break;

        //--Add all available units to the "After Turn List" so that it can perform all after turn functions such as weather damage, status damage, etc. in the appropriate speed order.
        List<BattleUnit> afterTurnList = new();
        var availPlayerUnits = _playerUnits.Select( u => u ).Where( u => u.Pokemon != null ).ToList();
        var availEnemyUnits = _enemyUnits.Select( u => u ).Where( u => u.Pokemon != null ).ToList();

        for( int i = 0; i < availPlayerUnits.Count; i++)
            afterTurnList.Add( availPlayerUnits[i] );

        for( int i = 0; i < availEnemyUnits.Count; i++)
            afterTurnList.Add( availEnemyUnits[i] );

        //--Sort all units by speed
        afterTurnList = afterTurnList.OrderByDescending( unit => unit.Pokemon.Speed ).ToList();

        //--Weather Prompts, will maybe move
        if( Field.Weather != null )
        {
            if( Field.WeatherDuration > 0 )
            {
                if( Field.Weather?.EffectMessage != null )
                {
                    string message = Field.Weather?.EffectMessage;
                    AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( message ) );
                }

                Debug.Log( $"Reducing {Field.Weather?.ID}'s Time Left from {Field.WeatherDuration} to {( Field.WeatherDuration - 1 )}" );
                Field.WeatherDuration--;
            }
            else if( Field.WeatherDuration == 0 )
            {
                if( Field.Weather?.EndMessage != null )
                {
                    string message = Field.Weather?.EndMessage;
                    AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( message ) );
                    yield return WaitForUIQueue();
                }

                //--If the route has a default weather (or eventually an active weather as part of a global weather system or something)
                //--we set the battlefield's weather to the default weather, without a duration since it should just continue until another
                //--weather overrides it (or the route's weather ends if that's a thing)
                //--else we set the weather to None, which the weather controller handles as well, and clear the id and duration.
                if( WeatherController.Instance.CurrentListener != null && WeatherController.Instance.CurrentListener.DefaultAreaWeather != WeatherConditionID.NONE )
                {
                    Field.SetWeather( WeatherController.Instance.CurrentListener.DefaultAreaWeather );
                    Field.WeatherDuration = null;

                    if( Field.Weather?.StartMessage != null )
                    {
                        string message = Field.Weather?.StartMessage;
                        AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( message ) );
                        yield return WaitForUIQueue();
                    }
                }
                else
                {
                    Field.SetWeather( WeatherConditionID.NONE );
                    Field.Weather = null;
                    Field.WeatherDuration = null;
                }
            }

            yield return WaitForUIQueue();
        }

        //--Theoretically there should always be courts active, they just might not have any conditions in their dictionaries
        if( Field.ActiveCourts.Count > 0 )
        {
            List<( CourtLocation location, CourtConditionID conditionID )> courtConditionsToRemove = new();
            foreach( var activeCourt in Field.ActiveCourts )
            {
                var court = activeCourt.Value;

                if( court.Conditions.Count > 0 )
                {
                    foreach( var courtCondition in court.Conditions )
                    {
                        var condition = courtCondition.Value;

                        if( condition.IsInfinite )
                        {
                            continue;
                        }
                        else if( condition.TimeLeft > 0 )
                        {
                            Debug.Log( $"Reducing {court}'s {condition.ID}'s Time Left from {condition.TimeLeft} to {( condition.TimeLeft - 1 )}" );
                            condition.TimeLeft--;
                        }
                        else if( condition.TimeLeft == 0 )
                        {
                            if( condition.EndMessage != null )
                            {
                                condition?.OnEnd?.Invoke( this, Field );
                                string message = condition.EndMessage;
                                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( message ) );
                                yield return WaitForUIQueue();
                            }

                            foreach( var unit in court.Units )
                            {
                                condition?.OnExitCourt?.Invoke( unit, Field );
                                yield return WaitForUIQueue();
                            }

                            courtConditionsToRemove.Add( ( activeCourt.Key, courtCondition.Key ) );
                        }
                    }
                }

            }

            if( courtConditionsToRemove.Count > 0 )
            {
                foreach( var (location, conditionID) in courtConditionsToRemove )
                {
                    Field.ActiveCourts[location].RemoveCondition( conditionID );
                }
            }
        }

        //--Go through each phase in the phase list, executing that phase on all pokemon in speed order
        foreach( RoundEndPhaseSO phaseSO in _roundEndPhase.RoundEndPhases )
        {
            var phase = _roundEndPhase.RoundEndPhaseDictionary[phaseSO.Type];

            foreach( var unit in afterTurnList )
            {
                if( unit.Pokemon.CurrentHP > 0 )
                {
                    phase.Apply( this, unit );
                    yield return ShowStatusChanges( unit );
                    yield return WaitForUIQueue();
                    yield return new WaitForSeconds( 0.25f );
                }

                yield return CheckForFaint( unit );

                if( unit.Pokemon.CurrentHP == 0 )
                {
                    if( BottomTrainerParty.Party.Contains( unit.Pokemon ) )
                    {
                        var activePokemon = _playerUnits.Select( u => u.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
                        var nextPokemon = BottomTrainerParty.GetHealthyPokemon( dontInclude: activePokemon );

                        if( CheckForBattleOver( activePokemon, nextPokemon ) )
                            break;
                    }

                    if( BattleType != BattleType.WildBattle_1v1 )
                    {
                        if( TopTrainerParty != null && TopTrainerParty.Party.Contains( unit.Pokemon ) )
                        {
                            var activeEnemyPokemon = _enemyUnits.Select( u => u.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
                            var nextEnemyPokemon = TopTrainerParty.GetHealthyPokemon( dontInclude: activeEnemyPokemon );

                            if( CheckForBattleOver( activeEnemyPokemon, nextEnemyPokemon ) )
                                break;
                        }
                    }
                    else
                    {
                        //--In a wild battle, if the enemy unit faints the battle must just end and nothing else should calc or tick
                        yield return HandleFaintedPokemon( unit );
                        yield break;
                    }
                }
            }
        }

        //--Handle Unit Flags
        //--Handle fainted Pokemon after all other phases are complete
        foreach( var unit in afterTurnList )
        {
            unit.SetFlagActive( UnitFlags.DidDamage, false );
            unit.SetFlagActive( UnitFlags.Phased, false );
            unit.Pokemon.CureTransientStatus();

            if( unit.Pokemon.IsFainted() )
                yield return HandleFaintedPokemon( unit );

            unit.IncreaseTurnsTakenInBattle();
        }

        yield return null;
    }

    //--Check a Move's accuracy and determine if it hits or misses
    private bool CheckMoveAccuracy( Move move, BattleUnit attacker, BattleUnit target ){
        if( move.MoveSO.Alwayshits )
            return true;

        if( move.MoveType == PokemonType.Poison && move.MoveSO.Name == "Toxic" && attacker.Pokemon.CheckTypes( PokemonType.Poison ) )
            return true;

        float moveAccuracy = move.MoveSO.Accuracy;

        int accuracy = attacker.Pokemon.StatStage[ Stat.Accuracy ];
        int evasion = target.Pokemon.StatStage[ Stat.Evasion ];

        var modifierValue = new float[] { 1f, 4f / 3f, 5f / 3f, 2f, 7f / 3f, 8f / 3f, 3f };

        // evasion = Mathf.FloorToInt( target.Pokemon.Modify_EVA( evasion, attacker.Pokemon, move ) );

        if( accuracy > 0 )
            moveAccuracy *= modifierValue[accuracy];
        else
            moveAccuracy /= modifierValue[-accuracy];

        if( evasion < 0 )
            moveAccuracy /= modifierValue[evasion];
        else
            moveAccuracy *= modifierValue[-evasion];

        moveAccuracy = Mathf.FloorToInt( attacker.Pokemon.Modify_ACC( moveAccuracy, target.Pokemon, move ) );

        return UnityEngine.Random.Range( 1, 101 ) <= moveAccuracy;
    }

    //--If a Move has secondary effects, apply them appropriately
    private IEnumerator RunMoveEffects( Move move, MoveEffects effects, MoveTarget moveTarget, BattleUnit attacker, BattleUnit target ){

        //--Modify Stats
        if( effects.StatChangeList != null ){
            if( moveTarget == MoveTarget.Self )
                attacker.Pokemon.ApplyStatStageChange( effects.StatChangeList, attacker.Pokemon ); //--Apply stat change to self, like ddance or swords dance
            else
                target.Pokemon.ApplyStatStageChange( effects.StatChangeList, attacker.Pokemon ); //--Apply stat change to target, like growl or tail whip
        }

        yield return ShowStatusChanges( attacker );
        yield return null;
        yield return ShowStatusChanges( target );
        yield return WaitForUIQueue();

        //--Apply Severe Status Effects
        if( effects.SevereStatus != StatusConditionID.NONE ){
            if( effects.SevereStatus == StatusConditionID.BRN && target.Pokemon.CheckTypes( PokemonType.Fire ) )
            {
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{target.Pokemon.NickName} cannot be burned!" ) );
                yield return WaitForUIQueue();
            }
            else
                target.Pokemon.SetSevereStatus( effects.SevereStatus ); //--Severe status like BRN, FRZ, PSN
        }

        yield return ShowStatusChanges( attacker );
        yield return null;
        yield return ShowStatusChanges( target );
        yield return WaitForUIQueue();

        //--Apply Volatile Status Effects
        if( effects.VolatileStatus != StatusConditionID.NONE ){
            target.Pokemon.SetVolatileStatus( effects.VolatileStatus ); //--Volatile status like CONFUSION
        }

        yield return ShowStatusChanges( attacker );
        yield return null;
        yield return ShowStatusChanges( target );
        yield return WaitForUIQueue();

        //--Apply Transient Status Effects
        if( effects.TransientStatus != StatusConditionID.NONE )
        {
            target.Pokemon.SetTransientStatus( effects.TransientStatus );
        }

        yield return ShowStatusChanges( attacker );
        yield return null;
        yield return ShowStatusChanges( target );
        yield return WaitForUIQueue();

        //--Start Weather Effects
        if( effects.Weather != WeatherConditionID.NONE ){
            var activePokemon = GetActivePokemon();

            //--First we call OnExitWeather while the previous weather was active, if there was one, so they can exit that weather (and lose their speed boosts, fuckers!)
            foreach( var unit in activePokemon )
            {
                Field.Weather?.OnExitWeather?.Invoke( unit.Pokemon );
                yield return null;
            }

            //--Wait for UI Queue just in case exiting a weather causes some UI event. I don't think it ever does? guess we'll see. --12/03/25
            yield return WaitForUIQueue();

            //--Then we set the new weather due to the move changing the weather
            Field.SetWeather( effects.Weather, 5 );
            AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( Field.Weather?.StartByMoveMessage ?? Field.Weather?.StartMessage ) );

            yield return WaitForUIQueue();
        }

        if( effects.CourtCondition != CourtConditionID.NONE )
        {
            CourtLocation location;

            Debug.Log( $"Running effect: {effects.CourtCondition}!" );

            if( _playerUnits.Contains( attacker ) )
            {
                if( moveTarget == MoveTarget.Enemy || moveTarget == MoveTarget.OpposingSide )
                    location = CourtLocation.TopCourt;
                else
                    location = CourtLocation.BottomCourt;
            }
            else
            {
                if( moveTarget == MoveTarget.Enemy || moveTarget == MoveTarget.OpposingSide )
                    location = CourtLocation.BottomCourt;
                else
                    location = CourtLocation.TopCourt;
            }

            Field.ActiveCourts[location]?.AddCondition( effects.CourtCondition );

            if( Field.ActiveCourts[location]?.Conditions[effects.CourtCondition]?.StartMessage != null )
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( Field.ActiveCourts[location]?.Conditions[effects.CourtCondition]?.StartMessage ) );
            
            if( effects.CourtCondition == CourtConditionID.TrickRoom )
            {
                if( !BattleFlags[BattleFlag.TrickRoom] )
                    AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( Field.ActiveCourts[location]?.Conditions[effects.CourtCondition]?.TrickRoomStartMessage?.Invoke( this, attacker.Pokemon ) ) );
                else
                    AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( Field.ActiveCourts[location]?.Conditions[effects.CourtCondition]?.TrickRoomAlreadyActiveMessage?.Invoke( this, attacker.Pokemon ) ) );
            }

            yield return WaitForUIQueue();

            Field.ActiveCourts[location]?.Conditions[effects.CourtCondition]?.OnStart?.Invoke( this, Field, location, attacker );

            foreach( var unit in Field.ActiveCourts[location].Units )
            {
                if( Field.ActiveCourts[location]?.Conditions[effects.CourtCondition]?.ConType != ConditionType.OpposingSide_Hazard )
                    Field.ActiveCourts[location]?.Conditions[effects.CourtCondition]?.OnEnterCourt?.Invoke( unit, Field );
            }

            yield return ShowStatusChanges( attacker );
            yield return null;
            yield return ShowStatusChanges( target );
            yield return null;
            yield return WaitForUIQueue();
            yield return null;
        }

        if( effects.SwitchEffect != null && effects.SwitchEffect?.SwitchType != SwitchEffectType.None )
        {
            var activePokemon = _playerUnits.Select( u => u.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
            var remainingPokemon = BottomTrainerParty.GetHealthyPokemon( dontInclude: activePokemon );

            var activeEnemyPokemon = _enemyUnits.Select( u => u.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
            Pokemon remainingEnemyPokemon = null;

            if( BattleType != BattleType.WildBattle_1v1 )
                remainingEnemyPokemon = TopTrainerParty.GetHealthyPokemon( dontInclude: activeEnemyPokemon );

            if( effects.SwitchEffect?.SwitchType == SwitchEffectType.SelfPivot )
            {
                Debug.Log( $"{attacker.Pokemon.NickName} is trying to Pivot out!" );

                if( _enemyUnits.Contains( attacker ) && remainingEnemyPokemon != null )
                {
                    if( attacker.IsAI )
                    {
                        var switchIn = attacker.BattleAI.RequestedForcedSwitch();
                        yield return PerformSwitchPokemonCommand( switchIn, attacker, true );
                    }
                    else
                        yield return ForcedSwitchPartyMenu( attacker ); //--Doing this is how we'll be able to setup same screen 2 human player battles :]
                }
                else if( _playerUnits.Contains( attacker ) && remainingPokemon != null )
                {
                    if( attacker.IsAI )
                    {
                        var switchIn = attacker.BattleAI.RequestedForcedSwitch();
                        yield return PerformSwitchPokemonCommand( switchIn, attacker, true );
                    }
                    else
                        yield return ForcedSwitchPartyMenu( attacker );
                }
            }
            
            if( effects.SwitchEffect?.SwitchType == SwitchEffectType.ForceOpponentOut )
            {
                Debug.Log( $"{attacker.Pokemon.NickName} is trying to force its opponent, {target.Pokemon.NickName}, out!" );

                if( remainingEnemyPokemon != null )
                {
                    if( target.IsAI )
                    {
                        var switchIn = target.BattleAI.RequestedForcedSwitch();
                        target.SetFlagActive( UnitFlags.Phased, true );
                        yield return PerformSwitchPokemonCommand( switchIn, target, true );
                    }
                    else
                    {
                        target.SetFlagActive( UnitFlags.Phased, true );
                        yield return ForcedSwitchPartyMenu( target );
                    }
                }
            }

            yield return ShowStatusChanges( attacker );
            yield return null;
            yield return ShowStatusChanges( target );
            yield return null;
            yield return WaitForUIQueue();
        }

        yield return ShowStatusChanges( attacker );
        yield return null;
        yield return ShowStatusChanges( target );
        yield return null;
        yield return WaitForUIQueue();
    }

    //--Display text update based on damage done
    private IEnumerator ShowDamageDetails( DamageDetails damageDetails ){
        //--critical hit dialogue
        if( damageDetails.Critical > 1 )
        {
            AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( "It was a critical hit!" ) );
            yield return WaitForUIQueue();
        }
    }
    private IEnumerator ShowTypeEffectiveness( float typeEffectiveness )
    {
        //--Super Effective dialogue
        if ( typeEffectiveness == 2f )
            AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( "It's super effective!" ) );

        if ( typeEffectiveness == 4f )
            AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( "It's extremely effective! " ) );

        //--Not Very Effective dialogue
        if ( typeEffectiveness == 0.5f )
            AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( "It wasn't very effective." ) );
        if ( typeEffectiveness == 0.25f )
            AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( "It had almost no effect!" ) );

        //--No Effect dialogue
        else if ( typeEffectiveness == 0 )
            AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( "It had no effect at all!" ) );

        yield return WaitForUIQueue();
    }

    private IEnumerator ShowStatusChanges( BattleUnit unit ){
        var pokemon = unit.Pokemon;

        while( pokemon.StatusChanges.Count > 0 ){
            var statusEvent = pokemon.StatusChanges.Dequeue();

            if( statusEvent.Type == StatusEventType.Damage )
            {
                AudioController.Instance.PlaySFX( SoundEffect.DamageEffective );
                yield return unit.PokeAnimator.PlayTakeDamageAnimation();
                yield return unit.BattleHUD.UpdateHPCoroutine();
            }

            if( statusEvent.Type == StatusEventType.SevereStatusDamage )
            {
                if( pokemon.SevereStatus != null && StatusIconAtlas.StatusIcons[pokemon.SevereStatus.ID].VFX != null )
                {
                    var vfxObj = Instantiate( StatusIconAtlas.StatusIcons[pokemon.SevereStatus.ID].VFX );
                    vfxObj.transform.SetPositionAndRotation( unit.PokeTransform.position, unit.PokeTransform.rotation );
                    Destroy( vfxObj, 2f );
                }

                AudioController.Instance.PlaySFX( SoundEffect.DamageEffective );
                yield return unit.PokeAnimator.PlayTakeDamageAnimation();
                yield return unit.BattleHUD.UpdateHPCoroutine();
            }

            if( statusEvent.Type == StatusEventType.SevereStatusPassive )
            {
                if( pokemon.SevereStatus != null && StatusIconAtlas.StatusIcons[pokemon.SevereStatus.ID].VFX != null )
                {
                    var vfxObj = Instantiate( StatusIconAtlas.StatusIcons[pokemon.SevereStatus.ID].VFX );
                    vfxObj.transform.SetPositionAndRotation( unit.PokeTransform.position, unit.PokeTransform.rotation );
                    Destroy( vfxObj, 2f );
                }
            }

            // Debug.Log( $"statusEvent.Message: {statusEvent.Message}" );
            if( statusEvent.Message != null || statusEvent.Message != string.Empty )
                if( pokemon.CurrentHP > 0 || pokemon.SevereStatus?.ID != StatusConditionID.FNT )
                    AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( statusEvent.Message ) );

            yield return null;
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
    public IEnumerator CheckForFaint( BattleUnit checkUnit ){
        // Debug.Log( $"CheckForFaint() on {checkUnit.Pokemon.NickName}" );
        if( checkUnit.Pokemon.CurrentHP > 0 || checkUnit.Pokemon.SevereStatus?.ID == StatusConditionID.FNT )
            yield break; //--if the pokemon's hp is above 0 we simply leave, it hasn't fainted yet. If the pokemon has already fainted from an earlier phase check, we also leave

        checkUnit.Pokemon.CureSevereStatus(); //--Clear any potential Severe Status, which would prevent FNT from being assigned
        checkUnit.Pokemon.CureVolatileStatus(); //--This also happens on faint, so it should be taken care of. Reminder to do so on switch too
        checkUnit.Pokemon.CureTransientStatus();
        checkUnit.ResetTurnsTakenInBattle();
        checkUnit.SetFlagActive( UnitFlags.ChoiceItem, false );
        checkUnit.SetLastUsedMove( null );

        checkUnit.Pokemon.SetSevereStatus( StatusConditionID.FNT ); //--Set fainted status condition
        yield return checkUnit.PokeAnimator.PlayFaintAnimation(); //--fainted animation placeholder

        if( checkUnit.Pokemon.IsPlayerUnit == true ){
            AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"Your {checkUnit.Pokemon.NickName} fainted!" ) );
            yield return WaitForUIQueue();
        }
        else if( checkUnit.Pokemon.IsEnemyUnit == true || checkUnit.Pokemon == checkUnit.Pokemon ){
            if( checkUnit.Pokemon == _wildPokemon ){
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"The wild {checkUnit.Pokemon.NickName} fainted!" ) );
                yield return WaitForUIQueue();
            }
            else if( BattleType == BattleType.TrainerSingles || BattleType == BattleType.TrainerDoubles || BattleType == BattleType.AI_Singles || BattleType == BattleType.AI_Doubles ){
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"The Enemy {checkUnit.Pokemon.NickName} fainted!" ) );
                yield return WaitForUIQueue();
            }
        }

        yield return null;
    }

    private bool CheckForBattleOver( List<Pokemon> activePokemon, Pokemon nextPokemon )
    {
        Debug.Log( $"[Battle System] Checking if the battle has ended..." );

        if( nextPokemon == null && activePokemon.Count == 0 )
        {
            Debug.Log( $"[Battle System] Battle is over!" );
            _battleOver = true;
            return true;
        }
        else
            return false;
    }

    private IEnumerator HandleFaintedPokemon( BattleUnit faintedUnit )
    {
        Debug.Log( $"[Battle System][Fainted Pokemon] {faintedUnit.Pokemon.NickName} has fainted!" );

        //--Remove the fainted unit's commands from the command queue, if it had any
        foreach( var command in _commandQueue )
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
            var nextPokemon = BottomTrainerParty.GetHealthyPokemon( dontInclude: activePokemon );

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
                    var switchIn = faintedUnit.BattleAI.RequestedForcedSwitch();
                    yield return PerformSwitchPokemonCommand( switchIn, faintedUnit, true );
                }
                else
                    yield return ForcedSwitchPartyMenu( faintedUnit );
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
                var remainingPokemon = TopTrainerParty.GetHealthyPokemon( dontInclude: activeEnemyPokemon );

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
                    var switchIn = faintedUnit.BattleAI.RequestedForcedSwitch();
                    yield return PerformSwitchPokemonCommand( switchIn, faintedUnit, true );
                }
            }
        }

        yield return ReorderCommands();

        yield return new WaitUntil( () => !_isForcedSwitch );
        yield return null;
    }

    //--When the player's pokemon faints, this is called explicitly, rather than as a command added to the command queue
    public IEnumerator PerformSwitchPokemonCommand( Pokemon pokemon, BattleUnit unit, bool forcedAI = false )
    {
        _battleStateEnum = BattleStateEnum.Busy;
        SetBusyState();

        if( BattleType != BattleType.AI_Singles && BattleType != BattleType.AI_Doubles )
        {
            var chosenMon = unit.Pokemon;
            var switchMon = pokemon;
            BottomTrainerParty.SwitchPokemonPosition( chosenMon, switchMon );
        }

        CourtLocation courtLocation = Field.GetUnitCourt( unit ).Location;
        Trainer trainer;

        if( courtLocation == CourtLocation.TopCourt )
            trainer = TopTrainer1;
        else
            trainer = BottomTrainer1;

        Debug.Log( $"[Switch Pokemon] Switching unit: {unit.Pokemon.NickName} for {pokemon.NickName} in the court: {courtLocation}" );

        unit.Pokemon.CureVolatileStatus(); //--Cure the volatile status of the previous pokemon. Will need to set a previous pokemon soon
        unit.ResetTurnsTakenInBattle();
        unit.Flags[UnitFlags.SuccessiveProtectUses].Count = 0;
        unit.SetFlagActive( UnitFlags.ChoiceItem, false );
        unit.SetLastUsedMove( null );

        if( !_isForcedSwitch )
        {
            if( !forcedAI )
            {
                AddToUIQueue( () => DialogueManager.Instance.PlayTrainerDialogueCR( $"{unit.Pokemon.NickName}, come back!", trainer ) );
                yield return WaitForUIQueue();
            }
        }

        if( courtLocation == CourtLocation.TopCourt )
            yield return unit.PokeAnimator.PlayExitBattleAnimation( TrainerCenter_Top1.transform );
        else
            yield return unit.PokeAnimator.PlayExitBattleAnimation( TrainerCenter_Bottom1.transform );

        //--Check for phase-out
        Debug.Log( $"AI {unit.Pokemon.NickName}'s Phased flag is: {unit.Flags[UnitFlags.Phased].IsActive}" );
        var isPhasedSwitch = unit.Flags[UnitFlags.Phased].IsActive;

        //--Raise OnExit for ability, weather, and held item conditions on the returning Pokemon
        unit.Pokemon.Ability?.OnAbilityExit?.Invoke( unit.Pokemon, _enemyUnits, Field );
        Field.Weather?.OnExitWeather?.Invoke( unit.Pokemon );
        unit.Pokemon.BattleItemEffect?.OnItemExit?.Invoke( unit );

        //--If the previous Pokemon exits while any court conditions are active on its side (Enemy = Top, Player = Bottom), raise OnExitCourt
        if( Field.ActiveCourts[courtLocation].Conditions.Count > 0 )
        {
            foreach( var condition in Field.ActiveCourts[courtLocation].Conditions )
            {
                condition.Value?.OnExitCourt?.Invoke( unit, Field );
                yield return WaitForUIQueue();
            }
        }

        var allyUnits = GetAllyUnits( unit );
        
        //--Grab the appropriate unit position and assign the incoming pokemon to it
        for( int i = 0; i < allyUnits.Count; i++)
        {
            if( allyUnits[i].Pokemon == unit.Pokemon )
            {
                unit.Setup( pokemon, allyUnits[i].BattleHUD, this ); //--Assign and setup the new pokemon
                unit.ResetTurnsTakenInBattle(); //--Sets turns taken to -1, so they may be incremented to 0 during RoundEndUpdate()
                unit.Flags[UnitFlags.SuccessiveProtectUses].Count = 0;
            }
        }

        AddToUIQueue( () => DialogueManager.Instance.PlayTrainerDialogueCR( $"Go, {pokemon.NickName}!", trainer ) );
        yield return WaitForUIQueue();
        AudioController.Instance.PlaySFX( SoundEffect.BattleBallThrow );

        if( courtLocation == CourtLocation.TopCourt )
            yield return unit.PokeAnimator.PlayEnterBattleAnimation( unit.transform, TrainerCenter_Top1.transform );
        else
            yield return unit.PokeAnimator.PlayEnterBattleAnimation( unit.transform, TrainerCenter_Bottom1.transform );

        yield return new WaitForSeconds( 0.25f );

        if( isPhasedSwitch )
            unit.Pokemon.SetTransientStatus( StatusConditionID.Phased );

        //--Call OnEnterCourt from all existing court conditions on the incoming pokemon
        if( Field.ActiveCourts[courtLocation].Conditions.Count > 0 )
        {
            foreach( var condition in Field.ActiveCourts[courtLocation].Conditions )
            {
                condition.Value?.OnEnterCourt?.Invoke( unit, Field );
                yield return ShowStatusChanges( unit );
                yield return null;
                yield return WaitForUIQueue();
                yield return null;
                yield return CheckForFaint( unit );
                yield return null;
                yield return WaitForUIQueue();
            }

            yield return CheckForFaint( unit );
            yield return null;
            yield return WaitForUIQueue();

            if( unit.Pokemon.IsFainted() )
                yield return HandleFaintedPokemon( unit );

            yield return WaitForUIQueue();
        }

        //--Check if the Pokemon has a weather ability, and set it if so.
        //--Then we check if any active pokemon receive effects of the possible new weather, so we raise OnEnterWeather on all active units
        pokemon.Ability?.OnAbilityEnter?.Invoke( pokemon, _enemyUnits, Field );
        yield return ShowStatusChanges( unit );
        yield return null;
        yield return WaitForUIQueue();

        //--Apply a held item's entry effect, if it has one
        pokemon.BattleItemEffect?.OnItemEnter?.Invoke( unit ); //--we use battleUnit here because the swapped in Pokemon should now be swapped into the unit position after being setup earlier.
        yield return ShowStatusChanges( unit );
        yield return null;
        yield return WaitForUIQueue();

        if( _isForcedSwitch ){
            //--During a fainted switch, the menu gets paused, but because fainted
            //--switch happens after the command queue, there's never an opportunity for
            //--the menu to become unpaused, therefore it needs to happen here in this
            //--fainted switch conditional area
            SetForcedSwitch( false );
        }

        yield return new WaitForSeconds( 0.1f );
    }

    public IEnumerator PerformUseItemCommand( Pokemon pokemon, Item item ){
        var itemUsed = _playerInventory.UseItem( item, pokemon );

        if( itemUsed != null ){
            AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( itemUsed.UseText( pokemon ) ) );
        }
        else{
            AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( "It didn't have any effect!" ) );
        }

        yield return WaitForUIQueue();
        yield return null;
    }

    public IEnumerator PerformRunFromBattleCommand(){
        // Debug.Log( "You got away!" );
        yield return new WaitForSeconds( 1f );
        EndBattle();
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
        float a = ( 3 * pokemon.MaxHP - 2 * pokemon.CurrentHP ) * pokemon.PokeSO.CatchRate * pokeball.CatchRate * StatusConditionsDB.GetStatusBonus( pokemon.SevereStatus ) / ( 3 * pokemon.MaxHP );

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

//--------------------------------------------------------------------------------------------------------
//-------------------------------------[ COMMAND SYSTEM METHODS ]-----------------------------------------
//--------------------------------------------------------------------------------------------------------
    private IEnumerator NextUnitInSelection()
    {
        PlayerBattleMenu.OnPauseState?.Invoke();
        //--Eventually we'll delay for things like animations and trigger other visual setups, like potentially showing the chosen action above the previous unit
        //--these things might as well be coroutines since there'll be tweening involved, so the delay can be built into the general duration of them
        yield return new WaitForSeconds( 0.5f );

        if( ActivePlayerUnitsCount == _playerUnits.Count )
        {
            _unitInSelectionState++;

            if( _unitInSelectionState > ActivePlayerUnitsCount - 1 )
                _unitInSelectionState = Mathf.Clamp( _unitInSelectionState, 0, ActivePlayerUnitsCount - 1 );
            else
                PlayerBattleMenu.OnUnpauseState?.Invoke();
        }

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

    public void DetermineCommandOrder()
    {
        if( StateMachine.CurrentState == _aiTurnState )
            PopState();

        if( _battleFlags[BattleFlag.TrickRoom] )
            _commandList = _commandList.OrderByDescending( prio => prio.CommandPriority ).ThenByDescending( prio => prio.AttackPriority ).ThenBy( prio => prio.UnitAgility ).ToList();
        else
            _commandList = _commandList.OrderByDescending( prio => prio.CommandPriority ).ThenByDescending( prio => prio.AttackPriority ).ThenByDescending( prio => prio.UnitAgility ).ToList();

        for( int i = 0; i < _commandList.Count; i++ ){
            AddCommand( _commandList[i] );
        }

        _commandList.Clear();

        StartCoroutine( ExecuteCommandQueue() );
    }

    private IEnumerator ReorderCommands()
    {
        if( _battleFlags[BattleFlag.TrickRoom] )
            _commandList = _commandQueue.OrderByDescending( prio => prio.CommandPriority ).ThenByDescending( prio => prio.AttackPriority ).ThenBy( prio => prio.UnitAgility ).ToList();
        else
            _commandList = _commandQueue.OrderByDescending( prio => prio.CommandPriority ).ThenByDescending( prio => prio.AttackPriority ).ThenByDescending( prio => prio.UnitAgility ).ToList();

        _commandQueue.Clear();

        for( int i = 0; i < _commandList.Count; i++ ){
            AddCommand( _commandList[i] );
        }

        _commandList.Clear();

        yield return null;
    }

    private void RemoveUnitCommandFromQueue( BattleUnit unit )
    {
        List<IBattleCommand> newCommandList = new();

        foreach( var command in _commandQueue )
        {
            if( command.User != unit )
                newCommandList.Add( command );
        }

        _commandQueue.Clear();

        for( int i = 0; i < newCommandList.Count; i++ )
            AddCommand( newCommandList[i] );
    }

    public void SetMoveCommand( BattleUnit attacker, BattleUnit target, Move move, bool aiCommand = false ){
        _useMoveCommand = new UseMoveCommand( move, attacker, target, this );
        _commandList.Add( _useMoveCommand );

        if( !aiCommand )
        {
            OnCommandAdded?.Invoke();
            StartCoroutine( NextUnitInSelection() );
        }
    }

    public void SetUseItemCommand( BattleUnit user, Pokemon pokemon, Item item ){
        _useItemCommand = new UseItemCommand( this, user, pokemon, item );
        _commandList.Add( _useItemCommand );
        OnCommandAdded?.Invoke();
        StartCoroutine( NextUnitInSelection() );
    }

    public void SetSwitchPokemonCommand( Pokemon pokemon, BattleUnit unitPosition, bool aiSwitch = false ){
        _switchPokemonCommand = new SwitchPokemonCommand( pokemon, this, unitPosition, aiSwitch );
        _commandList.Add( _switchPokemonCommand );

        if( !aiSwitch )
        {
            OnCommandAdded?.Invoke();
            StartCoroutine( NextUnitInSelection() );
        }
    }

    public void SetRunFromBattleCommand( BattleUnit user ){
        _runFromBattleCommand = new RunFromBattleCommand( this, user );
        _commandList.Add( _runFromBattleCommand );
        OnCommandAdded?.Invoke();
        StartCoroutine( NextUnitInSelection() );
    }

    public void AddCommand( IBattleCommand command ){
        _commandQueue.Enqueue( command );
    }

    private void HandleDoublesTargetSwap()
    {
        if( _commandQueue.Peek() is UseMoveCommand )
            {
                var moveCommand = _commandQueue.Peek() as UseMoveCommand;
                if( moveCommand.Target.Pokemon.SevereStatus?.ID == StatusConditionID.FNT || moveCommand.Target.Pokemon == null )
                {
                    //--change target if current target is fainted. if the new target has also fainted, we remove the command from the queue.
                    if( moveCommand.Target == _enemyUnits[0] )
                    {
                        moveCommand.ChangeTarget( _enemyUnits[1] );
                        if( moveCommand.Target.Pokemon.SevereStatus?.ID == StatusConditionID.FNT || moveCommand.Target.Pokemon == null || moveCommand.Target.Pokemon.CurrentHP == 0 )
                            _commandQueue.Dequeue();
                    }
                    else if( moveCommand.Target == _enemyUnits[1] )
                    {
                        moveCommand.ChangeTarget( _enemyUnits[0] );
                        if( moveCommand.Target.Pokemon.SevereStatus?.ID == StatusConditionID.FNT || moveCommand.Target.Pokemon == null || moveCommand.Target.Pokemon.CurrentHP == 0 )
                            _commandQueue.Dequeue();
                    }
                    else if( moveCommand.Target == _playerUnits[0] )
                    {
                        moveCommand.ChangeTarget( _playerUnits[1] );
                        if( moveCommand.Target.Pokemon.SevereStatus?.ID == StatusConditionID.FNT || moveCommand.Target.Pokemon == null || moveCommand.Target.Pokemon.CurrentHP == 0 )
                            _commandQueue.Dequeue();
                    }
                }
            }
    }

    public IEnumerator ExecuteCommandQueue()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds( 0.25f );

        while( _commandQueue.Count > 0 )
        {
            if( _commandQueue.Count == 0 )
                break;
            
            var user = _commandQueue.Peek().User;
            yield return WaitForUIQueue();
            yield return null;

            Debug.Log( $"[Command Queue] Next Command is: {_commandQueue.Peek()} by {user.Pokemon.NickName}. Their HP: {user.Pokemon.CurrentHP}, Status: {user.Pokemon.SevereStatus?.ID}" );
            if( _commandQueue.Peek() is UseMoveCommand )
            {
                var moveCommand = _commandQueue.Peek() as UseMoveCommand;
                Debug.Log( $"[Command Queue] The next move command by {moveCommand.User.Pokemon.NickName} is {moveCommand.Move.MoveSO.Name}, targeting: {moveCommand.Target}" );
            }

            yield return null;
            if( user.Pokemon.IsFainted() )
                yield return _commandQueue.Dequeue();

            yield return null;
            if( _commandQueue.Count == 0 )
                break;

            if( BattleType == BattleType.TrainerDoubles )
            {
                HandleDoublesTargetSwap();
                yield return null;
            }

            if( _commandQueue.Count > 0 )
            {
                Debug.Log( $"[Command Queue] Executing {_commandQueue.Peek()} by {user.Pokemon.NickName}. Their HP: {user.Pokemon.CurrentHP}, Status: {user.Pokemon.SevereStatus?.ID}" );
                yield return _commandQueue.Dequeue().ExecuteBattleCommand();
            }

            yield return new WaitUntil( () => !_battleComposer.CMBrain.IsBlending );
            yield return null;

            yield return AfterTurnUpdate( user );
            yield return null;

            yield return WaitForUIQueue();
            yield return new WaitForSeconds( 0.5f );

            //--We simply just reorder commands after every turn. with constant speed changes being fired off in an intense weather double battle, it's really not worth it to track a battle flag, or
            //--to give moves a flag to check for here. just do it anyway lol
            if( !_battleOver )
                yield return ReorderCommands();

            yield return null;
        }

        _commandQueue.Clear();
        yield return new WaitForSeconds( 0.25f );

        //--This should handle all board state updates like leftovers, status, weather, and field effects
        //--It gets called after all turns are completed and the command queue is empty
        yield return RoundEndUpdate();
        yield return null;
        
        yield return null;
        _unitInSelectionState = 0;

        yield return WaitForUIQueue();
        yield return new WaitForSeconds( 0.1f );

        if( BattleType == BattleType.AI_Singles || BattleType == BattleType.AI_Doubles )
        {
            yield return null;
            PushState( _aiTurnState );
        }
        else
            PushState( _actionSelectState );
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
