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

public enum BattleType { WildBattle_1v1, WildBattle_2v2, TrainerSingles, TrainerDoubles, TrainerMulti_2v1, TrainerMulti_2v2, }

[Serializable]
public class BattleSystem : MonoBehaviour
{
    public StateStackMachine<BattleSystem> StateMachine;
    [SerializeField] private State<BattleSystem> _actionSelectState;
    [SerializeField] private State<BattleSystem> _aiTurnState;
    [SerializeField] private State<BattleSystem> _turnManagerState;
    [SerializeField] private State<BattleSystem> _busyState;
    [SerializeField] private State<BattleSystem> _forceSelectPokemonState;
    [SerializeField] private State<BattleSystem> _roundEndPhaseState;

#region Private Serialized References
    //================================[ REFERENCES ]===========================================
    //--Serialized Fields/private-----------------------------------------
    [SerializeField] private BattleArena _battleArena;
    [SerializeField] private BattleComposer _battleComposer;
    [SerializeField] private GameObject _battleUnitPrefab;
    [SerializeField] private EventSystem _eventSystem;
    [SerializeField] private List<BattleHUD> _playerHUDs;
    [SerializeField] private List<BattleHUD> _enemyTrainerHUDs;
    [SerializeField] private BattleHUD _wildPokemonHUD;
    [SerializeField] private GameObject _enemyTrainerCanvas;
    [SerializeField] private GameObject _wildPokemonCanvas;
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
    private Queue<Func<IEnumerator>> _uiQueue;
#endregion
#region Public Getters and Properties
    //--public/getters/properties----------------------------------------------
    public static BattleSystem Instance;
    public Dictionary<BattleFlag, bool> BattleFlags => _battleFlags;
    public static bool BattleIsActive { get; private set; }
    public BattleComposer BattleComposer => _battleComposer;
    public EventSystem EventSystem => _eventSystem;
    public BattleType BattleType => _battleType;
    public BattleArena BattleArena => _battleArena;
    //--Units
    public GameObject Trainer1Center { get; private set; }
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
    private bool _isFaintedSwitch;
    private bool _isSinglesTrainerBattle;
    private bool _isDoublesTrainerBattle;
    private bool _levelUpCompleted;
    private bool _uiQueueEmpty;
    public bool IsSinglesTrainerBattle => _isSinglesTrainerBattle;
    public bool IsDoublesTrainerBattle => _isDoublesTrainerBattle;

#region Pokemon and Pokemon Parties
//=========================[ POKEMON AND PLAYER/TRAINER PARTIES ]================================================
    //--private
    private Trainer _enemyTrainer1;
    private Inventory _playerInventory;
    private PokemonParty _playerParty;
    private PokemonParty _enemyTrainerParty;
    private int _unitsInBattle;
    private int _unitInSelectionState = 0;
    private Pokemon _wildPokemon;
    private WildPokemon _encounteredPokemon; //--wild pokemon object that you ran into

    //--public/getters/properties
    public Inventory Inventory => _playerInventory;
    public PokemonParty PlayerParty => _playerParty;
    public PokemonParty EnemyTrainerParty => _enemyTrainerParty;
    public int UnitsInBattle => _unitsInBattle;
    public BattleUnit UnitInSelectionState => _playerUnits[_unitInSelectionState];
    public int ActivePlayerUnitsCount => _playerUnits.Count( u => u !=null && u.Pokemon != null && u.Pokemon.CurrentHP > 0 );
    public int ActiveEnemyUnitsCount => _enemyUnits.Count( u => u !=null && u.Pokemon != null && u.Pokemon.CurrentHP > 0 );
    public Pokemon WildPokemon => _wildPokemon;
    public WildPokemon EncounteredPokemon => _encounteredPokemon;
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

        DialogueManager.Instance.OnSystemDialogueComplete += SetLevelUpCompleted;

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
        StartCoroutine( UICoroutineRunner() );

        TotalPartyExpGain = 0;
        BattleIsActive = true;
        OnBattleStarted?.Invoke();
        AudioController.Instance.PlayMusic( MusicTheme.BattleThemeDefault, 10f );
    }

    private void OnDisable(){
        _roundEndPhase.Clear();
        Instance = null;
        DialogueManager.Instance.OnSystemDialogueComplete -= SetLevelUpCompleted;
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

    private void SetBusyState(){
        // Debug.Log( "SetBusyState" );
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
        _battleFlags = null;
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

    private void SetForceSelectPokemonState(){
        // Debug.Log( "SetForceSelectPokemonState" );
        if( PlayerBattleMenu.StateMachine.CurrentState == PlayerBattleMenu.PausedState )
            PlayerBattleMenu.OnChangeState?.Invoke( _pkmnMenu );
    }

    public void AddToUIQueue( Func<IEnumerator> cr, [System.Runtime.CompilerServices.CallerFilePath] string file = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0 )
    {
        _uiQueue.Enqueue( cr );
        Debug.Log( $"{_uiQueue.Peek()} was Enqueued from {System.IO.Path.GetFileName( file )}:{line}" );
    }

    private IEnumerator UICoroutineRunner()
    {
        while( true )
        {
            if( _uiQueue.Count > 0 )
            {
                _uiQueueEmpty = false;
                var next = _uiQueue.Dequeue();
                // Debug.Log( $"Starting UI Coroutine: {next()}" );
                yield return next();
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
        // yield return new WaitUntil( () => _uiQueue.Count == 0 );
        if( _uiQueue.Count > 0 )
            yield return new WaitUntil( () => _uiQueueEmpty ); //--TEST THIS FOR MOVE LEARNING AT THE END OF BATTLE. ALSO ANY ODD PLACES WHERE ACTION SELECT IS AVAILABLE BEFORE DIALOGUE COMPLETELY ENDS
        else
            yield return null;
    }

    //--Start setting up a battle. Anything that starts a battle needs to set the Battle Type. The Battle Type is responsible
    //--for HOW the Battle Stage will set itself up. From there, it will add all necessary unit positions to a list
    //--SOMEHOW, we will then assign all necessary Battle Unit objects from the Stage to their correct references here
    //--in the Battle System
    public void InitializeWildBattle( BattleType battleType ){
        _unitsInBattle = 1;
        _battleType = battleType;
        _playerParty = PlayerReferences.Instance.PlayerParty;
        _wildPokemonCanvas.SetActive( true );

        StartCoroutine( BattleSetup() );
    }

    public void InitializeTrainerSingles( Trainer trainer ){
        _unitsInBattle = 1;
        _isSinglesTrainerBattle = true;
        _enemyTrainer1 = trainer;
        _enemyTrainerCanvas.SetActive( true );
        

        //--Grab refs
        Trainer1Center = trainer.TrainerCenter;
        _battleType = trainer.BattleType;
        _playerParty = PlayerReferences.Instance.PlayerParty;
        _enemyTrainerParty = trainer.TrainerParty;

        StartCoroutine( BattleSetup() );
    }

    public void InitializeTrainerDoubles( Trainer trainer ){
        _unitsInBattle = 2;
        _isDoublesTrainerBattle = true;
        _enemyTrainer1 = trainer;
        _enemyTrainerCanvas.SetActive( true );

        for( int i = 0; i < _enemyTrainerHUDs.Count; i++ )
            _enemyTrainerHUDs[i].gameObject.SetActive( true );

        for( int i = 0; i < _playerHUDs.Count; i++ )
            _playerHUDs[i].gameObject.SetActive( true );

        Trainer1Center = trainer.TrainerCenter;
        _battleType = trainer.BattleType;
        _playerParty = PlayerReferences.Instance.PlayerParty;
        _enemyTrainerParty = trainer.TrainerParty;

        StartCoroutine( BattleSetup() );
    }

    private IEnumerator BattleSetup()
    {
        _unitInSelectionState = 0;
        BattleUIActions.OnBattleSystemBusy?.Invoke();
        PushState( _busyState );
        yield return null;
        yield return BattleArena.PrepareArena( this );
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
                // foreach( var unitAffectedByWeather in activePokemon )
                // {
                //     Field.Weather?.OnEnterWeather?.Invoke( unitAffectedByWeather.Pokemon );
                //     yield return WaitForUIQueue();
                // }
            }

            //--Turns taken in battle is defaulted to -1 so that we can simply increment a newly switched in pokemon at the very end of a round
            //--along side every other pokemon on the field. this would make a newly swapped in pokemon have taken 0 turns in battle
            //--for the first turn you can actually choose a command for them, which is correct.
            unit.ResetTurnsTakenInBattle(); //--Sets turns taken to -1
            unit.IncreaseTurnsTakenInBattle(); //--Since this is the beginning of a battle, the end of a round hasn't happened yet, so we increment here in its place, setting each turn taken to 0
            yield return new WaitForSeconds( 0.25f );
        }

        yield return new WaitForSeconds( 0.25f );
        PushState( _actionSelectState );
    }

    public void TriggerAbilityCutIn( Pokemon pokemon )
    {
        AddToUIQueue( () => _abilityCutIn.CutIn( pokemon.Ability?.Name, pokemon.NickName, Field.GetUnitCourt( pokemon ).Location ) );
    }

    private IEnumerator FaintedSwitchPartyMenu( BattleUnit unitPosition ){
        _isFaintedSwitch = true;
        SwitchUnitToPosition = unitPosition;
        SetForceSelectPokemonState();
        OnPlayerPokemonFainted?.Invoke();
        BattleUIActions.OnSubMenuOpened?.Invoke();
        BattleUIActions.OnPkmnMenuOpened?.Invoke();
        yield return new WaitUntil( () => !_isFaintedSwitch );
        yield return new WaitForSeconds( 0.1f );
        yield return null;
    }
    
    public void SetFaintedSwitchMon( Pokemon switchedMon, BattleUnit unitPosition ){
        //--The pkmn menu pops its own state before this function is called, therefore we need to
        //--Set ourselves back to the busy state.
        StartCoroutine( PerformSwitchPokemonCommand( switchedMon, unitPosition ) );
    }

    private IEnumerator SetForcedSwitchMon( Pokemon switchedMon, BattleUnit unitPosition ){
        //--for when something like u-turn or roar happens and we need
        //--to include it as part of the battle phase coroutine chain
        yield return PerformSwitchPokemonCommand( switchedMon, unitPosition );
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

    //--Will call this where necessary in HandleFaintedPokemon()
    //--We won't be applying exp directly to pokemon immediately during battle. instead,
    //--The entire party will gain the combined EXP after battle is over. mons that participated
    //--at all will gain full exp, mons that did not will gain, for now, 75%
    //--I may actually need to place a method that actually applies the exp into the PokemonParty script
    //--since it will occur after battle. or, just have a "post battle" state before the battle system
    //--ends. that sounds more appropriate
    private IEnumerator HandleExpGain( BattleUnit faintedUnit ){
        //--Exp Gain
        int expYield = faintedUnit.Pokemon.PokeSO.ExpYield;
        int unitLevel = faintedUnit.Pokemon.Level;
        float trainerBonus = ( _isSinglesTrainerBattle || _isDoublesTrainerBattle ) ? 1.5f : 1f;

        int expGain = Mathf.FloorToInt( expYield * unitLevel * trainerBonus ) / 7;

        //--Effort Points Gain
        int effortYield = faintedUnit.Pokemon.PokeSO.EffortYield;

        //--Add to totals
        TotalPartyExpGain += expGain;
        TotalPartyEffortGain += effortYield;

        yield return null; //--temp so unity shuts up
    }

    public void SetLevelUpCompleted( bool isComplete ){
        _levelUpCompleted = isComplete;
        // Debug.Log( "is level up completed: " + _levelUpCompleted );
    }

    private IEnumerator PostBattleScreen( Pokemon caughtPokemon = null, PokeBallType ball = PokeBallType.PokeBall ){
        _battleStateEnum = BattleStateEnum.Busy;
        SetBusyState();
        WaitForSeconds wait = new( 1f );

        //--Add Total Gained Exp. Eventually account for battle participation
        if( TotalPartyExpGain > 0 ){
            yield return GivePartyExp( wait );
        }

        //--Add Caught Pokemon to Party here AFTER exp calculations, so it doesn't gain EXP from itself being caught LOL
        if( caughtPokemon != null){
            _playerParty.AddPokemon( caughtPokemon, ball );
        }

        //--Post Trainer Battle
        if( _isSinglesTrainerBattle ){
            _enemyTrainer1.SetDefeated();
        }

        yield return wait;
        EndBattle();
    }

    private IEnumerator GivePartyExp( WaitForSeconds wait ){
        //--Gain Exp Dialogue
        AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"All Pokemon received {TotalPartyExpGain} Exp!" ) );
        yield return WaitForUIQueue();

        AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"All Pokemon received {TotalPartyEffortGain} Effort Points!" ) );
        yield return WaitForUIQueue();

        //--Give Exp to each Pokemon in player's party directly
        foreach( Pokemon pokemon in PlayerReferences.Instance.PlayerParty.PartyPokemon ){

            //--Gain EXP
            pokemon.GainExp( TotalPartyExpGain, TotalPartyEffortGain );

            //--If the current Pokemon is the Active Pokemon, refresh the BattleHUD
            for( int i = 0; i < _playerUnits.Count; i++ )
            {
                if( pokemon == _playerUnits[i].Pokemon )
                    yield return _playerHUDs[i].SetExpSmooth();
            }

            //--Check for Level up
            while( pokemon.CheckForLevelUpBattle() ){
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{pokemon.NickName} grew to level {pokemon.Level}!" ) );
                yield return WaitForUIQueue();

                //--Try Learn Moves //--i gotta get rid of this shit and move the post battle exp gain shit out of battle and remove the stoppage of play, like in time stranger, a game that did it after i wanted to.
                if( pokemon.GetNextLearnableMove() != null ){
                    var newMove = pokemon.GetNextLearnableMove();
                    if( !pokemon.CheckHasMove( newMove.MoveSO ) )
                    {
                        if( !pokemon.TryLearnMove( newMove.MoveSO ) ){
                            bool moveLearnOver = false;
                            bool learnedNewMove = false;

                            Action<bool> onMoveLearnComplete = ( bool pokemonLearnedNewMove ) =>
                            {
                                //--This callback is retrieving two things:
                                //--It's telling us that the move learn state is over, for the coroutine to continue
                                //--and the bool it takes is receiving whether or not the move was actually learned
                                moveLearnOver = true;
                                learnedNewMove = pokemonLearnedNewMove;
                            };

                            AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{pokemon.NickName} has learned {newMove.MoveSO.Name}!" ) );
                            AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"But it can't use more than four moves during battle. Will you swap it with an active move?" ) );
                            yield return null;
                            yield return WaitForUIQueue();

                            pokemon.TryReplaceMove( newMove.MoveSO, _learnMoveMenu, onMoveLearnComplete, this );
                            yield return new WaitUntil( () => moveLearnOver );

                            if( learnedNewMove ){
                                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{pokemon.NickName} added {newMove.MoveSO.Name} to its Current Moves!" ) );
                                yield return WaitForUIQueue();
                            }
                            else{
                                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{pokemon.NickName} added {newMove.MoveSO.Name} to its available moves!" ) );
                                yield return WaitForUIQueue();
                            }
                            
                        }
                        else{
                            AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{pokemon.NickName} learned {newMove.MoveSO.Name}!" ) );
                            yield return WaitForUIQueue();
                        }
                    }
                }

                //--Check for level up-based Evolution, eventually move out of battle system as levels will not be gained during battle
                var evolution = pokemon.CheckForEvolution();
                if( evolution != null && !pokemon.CanEvolveByLevelUp ){
                    pokemon.SetCanEvolveByLevelUp( true );
                    AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{pokemon.NickName} can now evolve!" ) );
                    yield return WaitForUIQueue();
                }

                //--If the current Pokemon is the Active Pokemon, refresh the BattleHUD
                yield return RefreshHUD( pokemon );

                yield return wait;

            }
        }
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
        StopCoroutine( WaitForUIQueue() );
        StopCoroutine( UICoroutineRunner() );
        _uiQueue.Clear();
        _uiQueue = null;

        ExitUnits(); //--Calls OnExit on active pokemon for weather and held items
        ClearCourts();

        if( BattleType == BattleType.WildBattle_1v1 ){
            _encounteredPokemon.Despawn();
            _encounteredPokemon = null;
        }

        if( BattleType == BattleType.TrainerSingles || BattleType == BattleType.TrainerDoubles )
            _enemyTrainerParty = null;

        _playerParty = null;

        _playerUnits = null;
        _enemyUnits = null;

        _isSinglesTrainerBattle = false;
        _isDoublesTrainerBattle = false;

        _commandQueue.Clear();
        _commandList = null;
        ClearBattleFlags();

        _battleArena.AfterBattleCleanup();
        _playerHUDs[1].gameObject.SetActive( false );
        _enemyTrainerHUDs[1].gameObject.SetActive( false );
        _wildPokemonCanvas.SetActive( false );
        _enemyTrainerCanvas.SetActive( false );

        TotalPartyExpGain = 0;
        TotalPartyEffortGain = 0;

        Field.SetWeather( WeatherConditionID.NONE );
        
        BattleUIActions.OnAttackPhaseCompleted?.Invoke();
        OnBattleEnded?.Invoke();

        BattleIsActive = false;
        GameStateController.Instance.GameStateMachine.Pop();
        AudioController.Instance.PlayMusic( AudioController.Instance.LastOverworldTheme, 5f );
    }

//-------------------------------------------------------------------------------------------------------
//--------------------------------------------[ COMMANDS ]-----------------------------------------------
//-------------------------------------------------------------------------------------------------------

    //--Perform any Move
    public IEnumerator PerformMoveCommand( Move move, BattleUnit attacker, BattleUnit target ){
        if( move.MoveSO.Name != "Protect" )
            attacker.Flags[UnitFlags.SuccessiveProtectUses].Count = 0;

        Debug.Log( $"{attacker.Pokemon.NickName} has used Protect: {attacker.Flags[UnitFlags.SuccessiveProtectUses].Count} times!" );

        //--Checks if there's a status impeding the pokemon from using a move this turn, such as sleep, flinch, first turn para, confusion, etc.
        bool canAttack = attacker.Pokemon.OnBeforeTurn();
        if( !canAttack ){
            yield return ShowStatusChanges( attacker );
            yield return WaitForUIQueue();
            yield return attacker.BattleHUD.WaitForHPUpdate();
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
                for( int i = 1; i <= hitAmount; i++ )
                {
                    if( move.MoveSO.MoveCategory == MoveCategory.Status ){
                        yield return _battleComposer.RunStatusAttackScene( move, attacker, target );
                        yield return RunMoveEffects( move.MoveSO.MoveEffects, move.MoveSO.MoveTarget, attacker, target );
                    }
                    else{
                        if( move.MoveSO.MoveCategory == MoveCategory.Physical )
                            yield return _battleComposer.RunPhysicalAttackScene( move, attacker, target );

                        if( move.MoveSO.MoveCategory == MoveCategory.Special )
                            yield return _battleComposer.RunSpecialAttackScene( move, attacker, target );
                        
                        attacker.SetFlagActive( UnitFlags.DidDamage, true );
                        damageDetails = target.TakeDamage( move, attacker, Field.Weather );
                        typeEffectiveness = damageDetails.TypeEffectiveness;
                        yield return _battleComposer.RunTakeDamagePhase( typeEffectiveness, target );

                        yield return target.BattleHUD.WaitForHPUpdate();
                        yield return ShowDamageDetails( damageDetails );
                    }

                    if( move.MoveSO.SecondaryMoveEffects != null && move.MoveSO.SecondaryMoveEffects.Count > 0 && target.Pokemon.CurrentHP > 0 ){
                        foreach( var secondary in move.MoveSO.SecondaryMoveEffects ){
                            var rand = UnityEngine.Random.Range( 1, 101 );
                            if( rand <= secondary.Chance )
                                yield return RunMoveEffects( secondary, secondary.Target, attacker, target );
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

                yield return ShowTypeEffectiveness( typeEffectiveness );

                if( hitAmount > 1 )
                    AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"The Pokemon was hit {hits} times!" ) );
                    yield return WaitForUIQueue();
            }
            else{
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

    public bool MoveSuccess( BattleUnit attacker, BattleUnit target, Move move, bool aiCheck = false )
    {
        if( move.MoveSO.Name == "Fake Out" )
        {
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
            if( target.Pokemon.TransientStatus.ID == StatusConditionID.Protect )
            {
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{target.Pokemon.NickName} protects itself!" ) );
                return false;
            }
        }
        
        //--This checks if the user fails or succeeds at USING Protect, based on Protect's successive use failure rate.
        if( move.MoveSO.Name == "Protect" )
        {
            int uses = attacker.Flags[UnitFlags.SuccessiveProtectUses].Count;
            float successChance = Mathf.Pow( 1f / 3f, uses );
            bool success;
            Debug.Log( $"{attacker.Pokemon.NickName}'s Protect success chance is: {successChance}" );
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

    //--Will eventually be adjusted, and expanded to include item, weather, field effect, and other necessary post-turn ticks
    //--MAKE A TURN MANAGER INSTEAD HE HE !
    private IEnumerator RoundEndUpdate(){
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

                // foreach( var unit in afterTurnList )
                // {
                //     Field.Weather?.OnExitWeather?.Invoke( unit.Pokemon );
                //     yield return WaitForUIQueue();
                // }

                //--If the route has a default weather (or eventually an active weather as part of a global weather system or something)
                //--we set the battlefield's weather to the default weather, without a duration since it should just continue until another
                //--weather overrides it (or the route's weather ends if that's a thing)
                //--else we set the weather to None, which the weather controller handles as well, and clear the id and duration.
                if( WeatherController.Instance.DefaultAreaWeather != WeatherConditionID.NONE )
                {
                    Field.SetWeather( WeatherController.Instance.DefaultAreaWeather );
                    Field.WeatherDuration = null;

                    if( Field.Weather?.StartMessage != null )
                    {
                        string message = Field.Weather?.StartMessage;
                        AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( message ) );
                        yield return WaitForUIQueue();
                    }

                    // foreach( var unit in afterTurnList )
                    // {
                    //     Field.Weather?.OnEnterWeather?.Invoke( unit.Pokemon );
                    //     yield return WaitForUIQueue();
                    // }
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
                    if( _playerParty.PartyPokemon.Contains( unit.Pokemon ) )
                    {
                        var activePokemon = _playerUnits.Select( u => u.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
                        var nextPokemon = _playerParty.GetHealthyPokemon( dontInclude: activePokemon );

                        if( CheckForBattleOver( activePokemon, nextPokemon ) )
                            break;
                    }

                    if( _enemyTrainerParty != null && _enemyTrainerParty.PartyPokemon.Contains( unit.Pokemon ) )
                    {
                        var activeEnemyPokemon = _enemyUnits.Select( u => u.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
                        var nextEnemyPokemon = _enemyTrainerParty.GetHealthyPokemon( dontInclude: activeEnemyPokemon );

                        if( CheckForBattleOver( activeEnemyPokemon, nextEnemyPokemon ) )
                            break;
                    }
                }
            }
        }

        //--Handle fainted Pokemon after all other phases are complete
        foreach( var unit in afterTurnList )
        {
            unit.SetFlagActive( UnitFlags.DidDamage, false );
            unit.Pokemon.CureTransientStatus();
            unit.IncreaseTurnsTakenInBattle();

            if( unit.Pokemon.IsFainted() )
                yield return HandleFaintedPokemon( unit );
        }

        yield return null;
    }

    //--Check a Move's accuracy and determine if it hits or misses
    private bool CheckMoveAccuracy( Move move, BattleUnit attacker, BattleUnit target ){
        if( move.MoveSO.Alwayshits )
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
    private IEnumerator RunMoveEffects( MoveEffects effects, MoveTarget moveTarget, BattleUnit attacker, BattleUnit target ){
        //--Modify Stats
        if( effects.StatChangeList != null ){
            if( moveTarget == MoveTarget.Self )
                attacker.Pokemon.ApplyStatStageChange( effects.StatChangeList, attacker.Pokemon ); //--Apply stat change to self, like ddance or swords dance
            else
                target.Pokemon.ApplyStatStageChange( effects.StatChangeList, attacker.Pokemon ); //--Apply stat change to target, like growl or tail whip
        }

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

        //--Apply Volatile Status Effects
        if( effects.VolatileStatus != StatusConditionID.NONE ){
            target.Pokemon.SetVolatileStatus( effects.VolatileStatus ); //--Volatile status like CONFUSION
        }

        //--Apply Transient Status Effects
        if( effects.TransientStatus != StatusConditionID.NONE )
        {
            target.Pokemon.SetTransientStatus( effects.TransientStatus );
        }

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

            //--THEN we call OnEnterWeather on every pokemon so they can enter the new weather
            // foreach( var unit in activePokemon )
            // {
            //     Field.Weather?.OnEnterWeather?.Invoke( unit.Pokemon );
            //     yield return null;
            // }

            yield return WaitForUIQueue();
        }

        if( effects.CourtCondition != CourtConditionID.NONE )
        {
            CourtLocation location;

            Debug.Log( $"Running effect: {effects.CourtCondition}!" );

            if( _playerUnits.Contains( attacker ) )
                location = CourtLocation.BottomCourt;
            else
                location = CourtLocation.TopCourt;

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

            Field.ActiveCourts[location]?.Conditions[effects.CourtCondition]?.OnStart?.Invoke( this, Field, location, attacker );

            foreach( var unit in Field.ActiveCourts[location].Units )
            {
                Field.ActiveCourts[location]?.Conditions[effects.CourtCondition]?.OnEnterCourt?.Invoke( unit, Field );
            }

            yield return WaitForUIQueue();
        }

        yield return ShowStatusChanges( attacker );
        yield return null;
        yield return ShowStatusChanges( target );
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

            Debug.Log( $"statusEvent.Message: {statusEvent.Message}" );
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
            else if( _isSinglesTrainerBattle ){
                AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"The Enemy {checkUnit.Pokemon.NickName} fainted!" ) );
                yield return WaitForUIQueue();
            }
        }

        yield return null;
    }

    private bool CheckForBattleOver( List<Pokemon> activePokemon, Pokemon nextPokemon )
    {

        if( nextPokemon == null && activePokemon.Count == 0 )
            return true;
        else
            return false;
    }

    private IEnumerator HandleFaintedPokemon( BattleUnit faintedUnit ){
        //--If Player Unit has fainted
        if( _playerUnits.Contains( faintedUnit ) )
        {

            //--For singles BattleTypes, we immediately clear the queue and let the player change pokemon
            //--if they have any more remaining in their party. if not, the battle ends, the player should be
            //--brought back to the last visited poke center
            var activePokemon = _playerUnits.Select( u => u.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
            var nextPokemon = _playerParty.GetHealthyPokemon( dontInclude: activePokemon );

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
                yield return FaintedSwitchPartyMenu( faintedUnit );
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
                var remainingPokemon = _enemyTrainerParty.GetHealthyPokemon( dontInclude: activeEnemyPokemon );

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
                    var switchIn = faintedUnit.BattleAI.RequestFaintedSwitch();
                    yield return PerformSwitchEnemyTrainerPokemonCommand( switchIn, faintedUnit );
                }
            }
        }

        yield return null;
    }

    public IEnumerator PerformSwitchEnemyTrainerPokemonCommand( Pokemon pokemon, BattleUnit unit ){
        //--This is currently only happening when the enemy trainer's pokemon faints and they have
        //--more left in their party. AI is eventually going to be expanded on to choose smarter
        //--pokemon instead of just the next one in their party, especially in the case of doubles.
        //--they will also be able to make smart switch calls during battle.
        //--I need to see how this functions when it's added as a BattleCommand, rather than just hard
        //--executed, but i can save that for another time, for now.
        _battleStateEnum = BattleStateEnum.Busy;
        SetBusyState();
        var courtLocation = CourtLocation.TopCourt;

        yield return unit.PokeAnimator.PlayExitBattleAnimation( Trainer1Center.transform );
        unit.Pokemon.CureVolatileStatus(); //--Cure the volatile status of the previous pokemon
        unit.ResetTurnsTakenInBattle();
        unit.Flags[UnitFlags.SuccessiveProtectUses].Count = 0;
        unit.SetFlagActive( UnitFlags.ChoiceItem, false );
        unit.SetLastUsedMove( null );

        //--Raise OnExit for ability, weather, and held item conditions on the returning Pokemon
        unit.Pokemon.Ability?.OnAbilityExit?.Invoke( unit.Pokemon, _playerUnits, Field );
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
        
        //--Grab the appropriate unit position and assign the incoming pokemon to it
        for( int i = 0; i < _enemyUnits.Count; i++)
        {
            if( _enemyUnits[i].Pokemon == unit.Pokemon )
            {
                unit.Setup( pokemon, _enemyTrainerHUDs[i], this ); //--Assign and setup the new pokemon
                unit.ResetTurnsTakenInBattle(); //--Sets turns taken to -1, so they may be incremented to 0 during RoundEndUpdate()
                unit.Flags[UnitFlags.SuccessiveProtectUses].Count = 0; //--Just in case. I am a deeply mistrusting person.
            }
        }

        AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"Go, {pokemon.NickName}!" ) );
        yield return WaitForUIQueue();
        AudioController.Instance.PlaySFX( SoundEffect.BattleBallThrow );
        yield return unit.PokeAnimator.PlayEnterBattleAnimation( unit.transform, Trainer1Center.transform );

        //--Call OnEnterCourt from all existing court conditions on the incoming pokemon
        if( Field.ActiveCourts[courtLocation].Conditions.Count > 0 )
        {
            foreach( var condition in Field.ActiveCourts[courtLocation].Conditions )
            {
                condition.Value?.OnEnterCourt?.Invoke( unit, Field );
                yield return WaitForUIQueue();
            }
        }

        // var activePokemon = GetActivePokemon();
        // var currentWeather = Field.Weather?.ID;
        //--Check if the Pokemon has an enter ability, and trigger it if so.
        //--Then we check if any active pokemon receive effects of the possible new weather, so we raise OnEnterWeather on all active units
        pokemon.Ability?.OnAbilityEnter?.Invoke( pokemon, _playerUnits, Field );
        yield return null;
        yield return WaitForUIQueue();

        // foreach( var activeUnit in activePokemon )
        // {
        //     activeUnit.Pokemon.Ability?.OnWeatherChange?.Invoke( activeUnit, Field );
        //     Field.Weather?.OnEnterWeather?.Invoke( activeUnit.Pokemon );
        //     yield return WaitForUIQueue();
        //     yield return new WaitForSeconds( 0.1f );
        // }

        //--Apply a held item's entry effect, if it has one, or Start the effect of one that triggers via OnStart instead of OnEnter
        pokemon.BattleItemEffect?.OnItemEnter?.Invoke( unit ); //--we use battleUnit here because the swapped in Pokemon should now be swapped into the unit position after being setup earlier.
        yield return null;
        yield return WaitForUIQueue();

    }

    //--When the player's pokemon faints, this is called explicitly, rather than as a command added to the command queue
    public IEnumerator PerformSwitchPokemonCommand( Pokemon pokemon, BattleUnit unit ){
        //--for the future if i want to target the previous pokemon with a move (pursuit), specificy the previous pokemon in this class? //--yes 4/12/2023
        var courtLocation = CourtLocation.BottomCourt;

        unit.Pokemon.CureVolatileStatus(); //--Cure the volatile status of the previous pokemon. Will need to set a previous pokemon soon
        unit.ResetTurnsTakenInBattle();
        unit.Flags[UnitFlags.SuccessiveProtectUses].Count = 0;
        unit.SetFlagActive( UnitFlags.ChoiceItem, false );
        unit.SetLastUsedMove( null );

        if( !_isFaintedSwitch )
        {
            AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{unit.Pokemon.NickName}, come back!" ) );
            yield return WaitForUIQueue();
        }

        yield return unit.PokeAnimator.PlayExitBattleAnimation( PlayerReferences.Instance.PlayerCenter );
        // _playerUnit.PokeAnimator.ResetAnimations(); //--Clears the animator which resets the animations before initialization of the incoming mon

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
        
        //--Grab the appropriate unit position and assign the incoming pokemon to it
        for( int i = 0; i < _playerUnits.Count; i++)
        {
            if( _playerUnits[i].Pokemon == unit.Pokemon )
            {
                unit.Setup( pokemon, _playerHUDs[i], this ); //--Assign and setup the new pokemon
                unit.ResetTurnsTakenInBattle(); //--Sets turns taken to -1, so they may be incremented to 0 during RoundEndUpdate()
                unit.Flags[UnitFlags.SuccessiveProtectUses].Count = 0;
            }
        }

        AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"Go, {pokemon.NickName}!" ) );
        yield return WaitForUIQueue();
        AudioController.Instance.PlaySFX( SoundEffect.BattleBallThrow );
        yield return unit.PokeAnimator.PlayEnterBattleAnimation( unit.transform, PlayerReferences.Instance.PlayerCenter );
        yield return new WaitForSeconds( 0.25f );

        //--Call OnEnterCourt from all existing court conditions on the incoming pokemon
        if( Field.ActiveCourts[courtLocation].Conditions.Count > 0 )
        {
            foreach( var condition in Field.ActiveCourts[courtLocation].Conditions )
            {
                condition.Value?.OnEnterCourt?.Invoke( unit, Field );
                yield return WaitForUIQueue();
            }
        }

        // var activePokemon = GetActivePokemon();
        // var currentWeather = Field.Weather?.ID;
        //--Check if the Pokemon has a weather ability, and set it if so.
        //--Then we check if any active pokemon receive effects of the possible new weather, so we raise OnEnterWeather on all active units
        pokemon.Ability?.OnAbilityEnter?.Invoke( pokemon, _enemyUnits, Field );
        yield return null;
        yield return WaitForUIQueue();

        // foreach( var activeUnit in activePokemon )
        // {
        //     activeUnit.Pokemon.Ability?.OnWeatherChange?.Invoke( activeUnit, Field );
        //     Field.Weather?.OnEnterWeather?.Invoke( activeUnit.Pokemon );
        //     yield return WaitForUIQueue();
        //     yield return new WaitForSeconds( 0.25f );
        // }

        //--Apply a held item's entry effect, if it has one
        pokemon.BattleItemEffect?.OnItemEnter?.Invoke( unit ); //--we use battleUnit here because the swapped in Pokemon should now be swapped into the unit position after being setup earlier.
        yield return null;
        yield return WaitForUIQueue();

        if( _isFaintedSwitch ){
            //--During a fainted switch, the menu gets paused, but because fainted
            //--switch happens after the command queue, there's never an opportunity for
            //--the menu to become unpaused, therefore it needs to happen here in this
            //--fainted switch conditional area
            _isFaintedSwitch = false;
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

    public void SetPlayerMoveCommand( BattleUnit attacker, BattleUnit target, Move move ){
        _useMoveCommand = new UseMoveCommand( move, attacker, target, this );
        _commandList.Add( _useMoveCommand );
        OnCommandAdded?.Invoke();
        StartCoroutine( NextUnitInSelection() );
    }

    //--Currently all player options lead to this, where the ai simply chooses a random move
    //--which then gets added to the command list, and then the priority sorting -> queue execution happens
    public void SetEnemyMoveCommand( BattleUnit attacker, BattleUnit target, Move move ){
        _useMoveCommand = new UseMoveCommand( move, attacker, target, this );
        _commandList.Add( _useMoveCommand );
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

    public IEnumerator ExecuteCommandQueue()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds( 0.25f );

        while( _commandQueue.Count > 0 )
        {
            var user = _commandQueue.Peek().User;
            yield return WaitForUIQueue();
            yield return null;

            Debug.Log( $"Executing next command. {user.Pokemon.NickName}'s HP: {user.Pokemon.CurrentHP}, Status: {user.Pokemon.SevereStatus?.ID}" );
            if( user.Pokemon.CurrentHP <= 0 || user.Pokemon.SevereStatus?.ID == StatusConditionID.FNT )
                yield return _commandQueue.Dequeue();

            if( _commandQueue.Count == 0 )
                break;
            
            if( _commandQueue.Peek() is UseMoveCommand && BattleType == BattleType.TrainerDoubles )
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

            if( _commandQueue.Count > 0 )
                yield return _commandQueue.Dequeue().ExecuteBattleCommand();

            yield return new WaitUntil( () => !_battleComposer.CMBrain.IsBlending );
            yield return null;
            user.Pokemon.BattleItemEffect?.OnItemAfterTurn?.Invoke( user );
            yield return ShowStatusChanges( user );
            yield return null;
            yield return WaitForUIQueue();
            yield return new WaitForSeconds( 0.5f );

            //--We simply just reorder commands after every turn. with constant speed changes being fired off in an intense weather double battle, it's really not worth it to track a battle flag, or
            //--to give moves a flag to check for here. just do it anyway lol
            yield return ReorderCommands();
            yield return null;
        }

        _commandQueue.Clear();
        yield return new WaitForSeconds( 0.25f );

        //--This should handle all board state updates like leftovers, status, weather, and field effects
        //--It gets called after all turns are completed and the command queue is empty
        yield return RoundEndUpdate();
        
        BattleUIActions.OnAttackPhaseCompleted?.Invoke();

        yield return null;
        _unitInSelectionState = 0;

        yield return WaitForUIQueue();
        yield return new WaitForSeconds( 0.1f );
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
