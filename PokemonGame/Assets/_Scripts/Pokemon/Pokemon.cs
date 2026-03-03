using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEditor;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using System.Threading;

[Serializable]
public class Pokemon
{
    [SerializeField] private string _pid;
    [SerializeField] private Gender _gender;
    [SerializeField] private PokemonSO _pokeSO;
    [SerializeField] private string _nickName;  
    [SerializeField] private int _level;
    [SerializeField] private NatureID _currentNature;
    [SerializeField] private NatureID _defaultNature;
    [SerializeField] private Ability _ability; //--Eventually will be a list of learned abilities?
    [SerializeField] private AbilityID _abilityID; //--For seeing current ability in inspector
    [SerializeField] private int _currentAbilityIndex = 0; //--Might change these to a singular AbilityID instead of playing around with indicies, let's see how the rest of the implementation goes...
    [SerializeField] private ItemSO _heldItem;    
    [SerializeField] private int _gainedEffortPoints;
    [SerializeField] private int _hpEVs = 0;
    [SerializeField] private int _atkEVs = 0;
    [SerializeField] private int _defEVs = 0;
    [SerializeField] private int _spatkEVs = 0;
    [SerializeField] private int _spdefEVs = 0;
    [SerializeField] private int _speEVs = 0;
//==============================================================================
    public string PID => _pid;
    public Gender Gender => _gender;
    public PokemonSO PokeSO => _pokeSO;
    public int Level => _level;
    public string NickName => _nickName;
    public Ability Ability => _ability;
    public AbilityID AbilityID => _abilityID;
    public int CurrentAbilityIndex => _currentAbilityIndex; //--Might change these to a singular AbilityID instead of playing around with indicies, let's see how the rest of the implementation goes...
    public ItemSO HeldItem => _heldItem;
    public int Exp { get; private set; }
    public BattleItemEffect BattleItemEffect { get; set; }
    public int Friendship { get; private set; }
    public bool CanEvolveByLevelUp { get; private set; }
    public int CurrentHP { get; set; }
    [SerializeField] private PokeBallType _currentBallType;
    public PokeBallType CurrentBallType => _currentBallType;
    public Sprite CurrentBallSprite => PokeBallIconAtlas.PokeBallIcons[CurrentBallType];
    [SerializeField] private List<Move> _activeMoves;
    [SerializeField] private List<Move> _learnedMoves;
    [SerializeField] private List<Move> _preSelectedMoves;
    public List<Move> ActiveMoves => _activeMoves;
    public List<Move> LearnedMoves => _learnedMoves;
    public Dictionary<Stat, int> Stats { get; private set; }
    public Dictionary<Stat, int> EffortValues { get; private set; } //--Consider moving EV variables to a dictionary instead!
    public Dictionary<Stat, int> StatStages { get; private set; }
    public Dictionary<Stat, Dictionary<DirectModifierCause, float>> DirectStatModifiers { get; private set; }
    public Dictionary<NatureID, Nature> Natures { get; private set; }
    public NatureID CurrentNature => _currentNature;
    public NatureID DefaultNature => _defaultNature;
    public SevereCondition SevereStatus { get; private set; }
    public Dictionary<VolatileConditionID, ( VolatileCondition Condition, int Duration )> VolatileStatuses { get; private set; }
    public TransientCondition TransientStatus { get; private set; }
    public Dictionary<BindingConditionID, ( BindingCondition Condition, int Duration )> BindingStatuses { get; private set; }
    public int SevereStatusTime { get; set; }
    public bool TransientStatusActive { get; set; }
    public Queue<StatusEvent> StatusChanges { get; private set; }
//==================[ Events ]===========================================
    public event Action OnStatusChanged;
    public event Action OnDisplayInfoChanged;

//--------------------------------------------------------------------------------------------
//-----------------------------------[POKEMON STATS]------------------------------------------
//--------------------------------------------------------------------------------------------

    //--Stats
    public int MaxHP =>         GetStat( Stat.HP );
    public int Attack =>        GetStat( Stat.Attack );
    public int Defense =>       GetStat( Stat.Defense );
    public int SpAttack =>      GetStat( Stat.SpAttack );
    public int SpDefense =>     GetStat( Stat.SpDefense );
    public int Speed =>         GetStat( Stat.Speed );

    //--Direct Stat Modifier (burn, paralyze, ruin abilities, etc. )
    public float HP_Modifier { get; private set; }


//--------------------------------------------------------------------------------------------
//------------------------------------[EFFORT VALUES]-----------------------------------------
//--------------------------------------------------------------------------------------------
    public int GainedEffortPoints => _gainedEffortPoints;
    public int RemainingEffortPoints { get; private set; }
    public int HP_EVs =>        GetEVs( Stat.HP );
    public int ATK_EVs =>       GetEVs( Stat.Attack );
    public int DEF_EVs =>       GetEVs( Stat.Defense );
    public int SPATK_EVs =>     GetEVs( Stat.SpAttack );
    public int SPDEF_EVs =>     GetEVs( Stat.SpDefense );
    public int SPE_EVs =>       GetEVs( Stat.Speed );

//--------------------------------------------------------------------------------------------
//------------------------------------[ CONSTRUCTORS ]----------------------------------------
//--------------------------------------------------------------------------------------------

    public Pokemon( PokemonSO pokeSO, int level )
    {
        _pokeSO = pokeSO;
        _level = level;

        Init();
    }

    public Pokemon( TrainerPokemon trainerPokemon )
    {
        _pokeSO = trainerPokemon.PokeSO;
        _level = trainerPokemon.Level;

        //--Nickname
        if( trainerPokemon.NickName != "" )
            _nickName = trainerPokemon.NickName;
        else
            _nickName = _pokeSO.Species;

        //--Nature
        InitializeNatures();
        _defaultNature = trainerPokemon.Nature;
        Debug.Log( $"[Pokemon][Nature] {NickName}'s Default Nature is: {_defaultNature}" );
        if( _defaultNature == NatureID.None )
            GetRandomNature();

        _currentNature = _defaultNature;
        AssignAbilityFromID( trainerPokemon.AbilityID );

        //--Held Item
        SetupBattleItem( trainerPokemon.HeldItem );

        //--Effort Points
        _hpEVs      = trainerPokemon.HP_EVs;
        _atkEVs     = trainerPokemon.Atk_EVs;
        _defEVs     = trainerPokemon.Def_EVs;
        _spatkEVs   = trainerPokemon.SpAtk_EVs;
        _spdefEVs   = trainerPokemon.SpDef_EVs;
        _speEVs     = trainerPokemon.Spe_EVs;
        InitializeEVs();

        //--Stats
        CalculateStats();
        ResetStatChanges();
        CurrentHP = MaxHP;

        //--Moves
        _activeMoves = new();
        for( int i = 0; i < trainerPokemon.Moves.Count; i++ )
        {
            if( trainerPokemon.Moves[i] == null )
                continue;
                
            Move move = new( trainerPokemon.Moves[i] );
            _activeMoves.Add( move );
        }

        //--Status
        SevereStatus = null;
        // VolatileStatus = new();
        VolatileStatuses = new();
        TransientStatus = null;
        BindingStatuses = new();

        StatusChanges = new Queue<StatusEvent>();

        _currentBallType = trainerPokemon.Ball;
        if( _currentBallType == PokeBallType.None )
            _currentBallType = PokeBallType.PokeBall;

        _pid = GeneratePID( 31, 31, 31, 31, 31, 31, (int)_defaultNature, _currentAbilityIndex, _pokeSO.Form );

        //--Events
        BattleSystem.OnBattleEnded += OnBattleEnded;
    }

    public Pokemon( PokemonSaveData saveData )
    {
        _pid                    = saveData.PID;
        _pokeSO                 = PokemonDB.GetPokemonBySpecies( ( saveData.Species, saveData.Form ) );
        _nickName               = saveData.NickName;
        _level                  = saveData.Level;
        _currentNature          = saveData.CurrentNature;
        _defaultNature          = saveData.DefaultNature;
        _currentAbilityIndex    = saveData.CurrentAbilityIndex;
        _heldItem               = ItemsDB.GetItemByName( saveData.HeldItem );
        Friendship              = saveData.Friendship;
        CanEvolveByLevelUp      = saveData.CanEvolveByLevelUp;
        CurrentHP               = saveData.CurrentHP;
        Exp                     = saveData.Exp;
        _gainedEffortPoints     = saveData.GainedEffortPoints;
        RemainingEffortPoints   = saveData.RemainingEffortPoints;
        _currentBallType        = saveData.CurrentBall;

        InitializeEVs();
        AssignEVs( Stat.HP,         saveData.HP_EVs, true );
        AssignEVs( Stat.Attack,     saveData.ATK_EVs, true );
        AssignEVs( Stat.Defense,    saveData.DEF_EVs, true );
        AssignEVs( Stat.SpAttack,   saveData.SPATK_EVs, true );
        AssignEVs( Stat.SpDefense,  saveData.SPDEF_EVs, true );
        AssignEVs( Stat.Speed,      saveData.SPE_EVs, true );

        GetCurrentAbilityFromIndex();
        SetupBattleItem();

        if( saveData.SevereStatus != null ){
            SevereStatus = SevereConditionsDB.Conditions[saveData.SevereStatus.Value];
            OnStatusChanged?.Invoke();
        }
        else
            SevereStatus = null;

        //--Restore Moves
        _activeMoves = saveData.ActiveMoves.Select( s => new Move( s ) ).ToList(); //--Active Moves
        _learnedMoves = saveData.LearnedMoves.Select( s => new Move( s ) ).ToList(); //--Full list of known moves, not including active moves. TODO

        CalculateStats();

        ResetStatChanges();
        VolatileStatuses = new();
        TransientStatus = null;
        BindingStatuses = new();

        StatusChanges = new Queue<StatusEvent>();
    }

//--------------------------------------------------------------------------------------------
//-------------------------------------[ FUNCTIONS ]------------------------------------------
//--------------------------------------------------------------------------------------------
    
    public void Init()
    {
        //--Set Name
        if(  string.IsNullOrEmpty( _nickName ) )
            _nickName = PokeSO.Species;

        InitializeNatures();
        Debug.Log( $"[Pokemon][Nature] {NickName}'s Default Nature is: {_defaultNature}" );
        if( _defaultNature == NatureID.None )
            GetRandomNature();

        _currentNature = _defaultNature;
        GetCurrentAbilityFromIndex();
        SetupBattleItem();

        Friendship = PokeSO.BaseFriendship;

        //--------GENERATE MOVES-----------
        bool populateActiveMoves = true;
        if( _preSelectedMoves != null && _preSelectedMoves.Count > 0 )
            populateActiveMoves = false;

        _activeMoves = new List<Move>();
        _learnedMoves = new List<Move>();
        foreach( var move in PokeSO.LearnableMoves )
        {
            //--Add moves from the most recent down, i think until we reach max active moves
            //--once we reach max active moves, if there's still more moves we should learn
            //--from our learnset via levelup, add them to the LearnedMoves list for access later
            if( move.LevelLearned <= Level && move.LevelLearned > 0 && ActiveMoves.Count < PokemonSO.MAX_ACTIVE_MOVES && populateActiveMoves )
                ActiveMoves.Add( new Move( move.MoveSO ) );
            else if( move.LevelLearned <= Level && ActiveMoves.Count == PokemonSO.MAX_ACTIVE_MOVES )
                LearnedMoves.Add( new Move( move.MoveSO ) );
        }

        if( !populateActiveMoves && _preSelectedMoves.Count > 0 )
        {
            foreach( var move in _preSelectedMoves )
            {
                ActiveMoves.Add( new( move.MoveSO ) );
            }
        }


        //--Exp
        if( Exp == 0 )
            Exp = PokeSO.GetExpForLevel( Level );

        //--Evolve by level Flag
        if( CheckForEvolution() != null )
            CanEvolveByLevelUp = true;

        //--Stats
        RemainingEffortPoints = _gainedEffortPoints;
        InitializeEVs();
        CalculateStats();
        ResetStatChanges();
        CurrentHP = MaxHP;

        //--Status
        SevereStatus = null;
        // VolatileStatus = new();
        VolatileStatuses = new();
        TransientStatus = null;
        BindingStatuses = new();

        StatusChanges = new Queue<StatusEvent>();

        if( _currentBallType == PokeBallType.None )
            _currentBallType = PokeBallType.PokeBall;

        _pid = GeneratePID( 31, 31, 31, 31, 31, 31, (int)_defaultNature, _currentAbilityIndex, _pokeSO.Form );
        
        //--Events
        BattleSystem.OnBattleEnded += OnBattleEnded;
    }

    public PokemonSaveData CreateSaveData()
    {
        var saveData = new PokemonSaveData()
        {
            PID = _pid,
            Species = _pokeSO.Species,
            Form = _pokeSO.Form,
            NickName = _nickName,
            Level = _level,
            CurrentNature = _currentNature,
            DefaultNature = _defaultNature,
            CurrentAbilityIndex = _currentAbilityIndex,
            HeldItem = _heldItem.ItemName,
            Friendship = Friendship,
            CanEvolveByLevelUp = CanEvolveByLevelUp,
            CurrentHP = CurrentHP,
            Exp = Exp,
            GainedEffortPoints = GainedEffortPoints,
            RemainingEffortPoints = RemainingEffortPoints,
            CurrentBall = CurrentBallType,
            HP_EVs = HP_EVs,
            ATK_EVs = ATK_EVs,
            DEF_EVs = DEF_EVs,
            SPATK_EVs = SPATK_EVs,
            SPDEF_EVs = SPDEF_EVs,
            SPE_EVs = SPE_EVs,
            SevereStatus = SevereStatus?.ID,
            ActiveMoves = ActiveMoves.Select( m => m.CreateSaveData() ).ToList(),
            LearnedMoves = LearnedMoves.Select( m => m.CreateSaveData() ).ToList(),
        };

        return saveData;
    }

    private void GetRandomNature()
    {
        int d21 = UnityEngine.Random.Range( 1, 22 );
        _defaultNature = (NatureID)d21;
        Debug.Log( $"[Pokemon][Nature] Random Nature, {_defaultNature} chosen for {NickName}" );
    }

    private void AssignAbilityFromID( AbilityID id )
    {
        Debug.Log( $"[Pokemon][Ability] Assigning {NickName}'s ability {id} from its id!" );
        if( PokeSO.Abilities.Count > 0 && PokeSO.Abilities.Contains( id ) )
        {
            Debug.Log( $"[Pokemon][Ability] id found in {PokeSO.Species} PokemonSO! Assigning..." );
            _ability = AbilityDB.Abilities[id];
            _abilityID = id;
            _currentAbilityIndex = PokeSO.Abilities.IndexOf( id );
            Debug.Log( $"[Pokemon][Ability] {NickName}'s ability info: Ability: {_ability}, ID: {_abilityID}, Index: {_currentAbilityIndex}" );
        }
        else if( PokeSO.Abilities.Count > 0 )
        {
            Debug.Log( $"[Pokemon][Ability] id not found in {PokeSO.Species} PokemonSO! Assigning first index!" );
            _currentAbilityIndex = 0;
            _ability = AbilityDB.Abilities[PokeSO.Abilities[_currentAbilityIndex]];
            _abilityID = PokeSO.Abilities[_currentAbilityIndex];
        }
        else
            Debug.Log( $"[Pokemon][Ability] NO abilities found in {PokeSO.Species} PokemonSO!" );
    }

    public void SkillSwap( AbilityID id )
    {
        _ability = AbilityDB.Abilities[id];
        _abilityID = id;
    }

    public void ResetSkillSwap()
    {
        GetCurrentAbilityFromIndex();
    }

    private void GetCurrentAbilityFromIndex()
    {
        if( PokeSO.Abilities.Count != 0 )
        {
            int i = _currentAbilityIndex;
            _ability = AbilityDB.Abilities[PokeSO.Abilities[i]];
            _abilityID = PokeSO.Abilities[i];
        }
    }

    public void SetupBattleItem()
    {
        if( _heldItem != null && _heldItem.HasBattleEffect )
            BattleItemEffect = BattleItemDB.BattleItemEffects[_heldItem.BattleEffectID];
    }

    public void SetupBattleItem( ItemSO item )
    {
        _heldItem = item;
        if( _heldItem != null && _heldItem.HasBattleEffect )
            BattleItemEffect = BattleItemDB.BattleItemEffects[_heldItem.BattleEffectID];
    }

    public void GiveHeldItem( ItemSO item )
    {
        if( item == null )
            return;
        
        Debug.Log( $"[Held Item] Giving {NickName} held item: {item}." );
        if( _heldItem != null )
            Debug.Log( $"[Held Item]{NickName} was holding: {_heldItem}!" );

        _heldItem = item;
        if( item.HasBattleEffect )
            BattleItemEffect = BattleItemDB.BattleItemEffects[_heldItem.BattleEffectID];

        Debug.Log( $"[Held Item] {NickName}'s held item now is: {_heldItem}" );
    }

    public void RemoveHeldItem()
    {
        _heldItem = null;
        BattleItemEffect = null;
    }

    public void ChangeNickName( string name )
    {
        _nickName = name;
    }

    public void ChangeCurrentBall( PokeBallType ball )
    {
        _currentBallType = ball;
        OnDisplayInfoChanged?.Invoke();
    }

    public bool CheckTypes( PokemonType type )
    {
        if( PokeSO.Type1 == type || PokeSO.Type2 == type )
            return true;

        return false;
    }

    public void GainExp( int gainedExp )
    {
        if( _level == PokemonSO.MAXLEVEL )
            return;
            
        Exp += gainedExp;
    }

    public void GainEP( int gainedEP )
    {
        if( _gainedEffortPoints >= 508 )
            return;

        _gainedEffortPoints += gainedEP;
        RemainingEffortPoints += gainedEP;
    }

    //--disabled for now just to shut up trylearnmove, will be used for out of battle leveling via candies n shit
    public bool CheckForLevelUp(){
        if( CanLevel() ){
            //--Learn Moves
            if( GetNextLearnableMove() != null ){
                var newMove = GetNextLearnableMove();
                if( !CheckHasMove( newMove.MoveSO ) )
                    TryLearnMove( newMove.MoveSO );

                return true;
            }
        }
        
        return false;
    }

    public bool CheckForLevelUpBattle(){
        return CanLevel();
    }

    public bool CanLevel(){
        if( _level == PokemonSO.MAXLEVEL )
            return false;

        if( Exp > _pokeSO.GetExpForLevel( _level + 1 ) ){
            int previousMaxHP = MaxHP;
            _level++;
            CalculateStats();
            UpdateHPOnLevelup( previousMaxHP );
            OnDisplayInfoChanged?.Invoke();
            return true;
        }
        else
            return false;
    }

    public void SetCanEvolveByLevelUp( bool canEvolve ){
        CanEvolveByLevelUp = canEvolve;
        OnDisplayInfoChanged?.Invoke();
    }

    public Evolutions CheckForEvolution(){
        return PokeSO.Evolutions.FirstOrDefault( e => e.EvolutionLevel <= _level && e.EvolutionLevel != 0 );
    }

    public Evolutions CheckForEvolution( ItemSO evoItem ){
        return PokeSO.Evolutions.FirstOrDefault( e => e.EvolutionItem == evoItem );
    }

    public void Evolve( Evolutions evo ){
        if( _nickName == _pokeSO.Species )
            _nickName = evo.Pokemon.Species;

        int previousMaxHP = MaxHP;
        _pokeSO = evo.Pokemon;
        CalculateStats();
        UpdateHPOnLevelup( previousMaxHP );
        OnDisplayInfoChanged?.Invoke();
    }

    public LearnableMoves GetNextLearnableMove(){
        return _pokeSO.LearnableMoves.Where( x => x.LevelLearned == _level ).FirstOrDefault();
    }

    public bool TryLearnMove( MoveSO newMove ){
        //--If active moves (max 4) is less than 4, simply add the move, return true so we can move on with life
        if( ActiveMoves.Count < PokemonSO.MAX_ACTIVE_MOVES ){
            ActiveMoves.Add( new Move( newMove ) );
            return true;
        }
        else
            LearnedMoves.Add( new Move( newMove ) );
            return false;
    }

    public void LearnLevelUpMove( MoveSO newMove )
    {
        if( ActiveMoves.Count < PokemonSO.MAX_ACTIVE_MOVES )
            ActiveMoves.Add( new Move( newMove ) );
        else
            LearnedMoves.Add( new Move( newMove ) );
    }

    public void TryReplaceMove( MoveSO newMove, ILearnMoveContext moveMenu, Action<bool> learnMoveComplete, BattleSystem battleSystem ){
        var moveMenuBattle = (LearnMove_Battle)moveMenu;
        moveMenuBattle.Setup( this, ActiveMoves.Select( x => x.MoveSO ).ToList(), newMove, learnMoveComplete );
        battleSystem.PlayerBattleMenu.OnPushNewState?.Invoke( battleSystem.PlayerBattleMenu.MoveLearnSelectionState );
    }

    public void TryReplaceMove( MoveSO newMove, ILearnMoveContext moveMenu, Action<bool> learnMoveComplete, BagScreen_Pause bagScreen ){
        var moveMenuPause = (LearnMove_Pause)moveMenu;
        moveMenuPause.Setup( this, ActiveMoves.Select( x => x.MoveSO ).ToList(), newMove, learnMoveComplete );
        bagScreen.PauseMenuStateMachine.PushState( bagScreen.LearnMoveMenu );
    }

    public void ReplaceWithNewMove( MoveSO replacedMove, MoveSO newMove )
    {
        for( int i = 0; i < ActiveMoves.Count; i++ ){
            if( ActiveMoves[i].MoveSO == replacedMove ){
                ActiveMoves[i] = new Move ( newMove );
                LearnedMoves.Add( new Move( replacedMove ) );
            }
        }
    }
    public bool CheckHasMove( MoveSO move )
    {
        return ActiveMoves.Count( m => m.MoveSO == move ) > 0 || _learnedMoves.Count( m=> m.MoveSO == move ) > 0;
    }

    public bool CheckHasMove( string move )
    {
        return ActiveMoves.Count( m => m.MoveSO.Name == move ) > 0 || _learnedMoves.Count( m=> m.MoveSO.Name == move ) > 0;
    }

    public bool CheckHasAttackingMoveOfType( PokemonType type )
    {
        for( int i = 0; i < _activeMoves.Count; i++ )
        {
            var move = _activeMoves[i];

            if( move.MoveType == type && move.MoveSO.MoveCategory != MoveCategory.Status )
                return true;
            else
                continue;
        }

        return false;
    }

    public bool CheckCanLearnTM( MoveSO move ){
        return PokeSO.TeachableMoves[move];
    }

    private void CalculateStats(){
        int iv = 31;

        Stats = new()
        {
            { Stat.HP,        Mathf.FloorToInt( ( ( 2 * PokeSO.MaxHP     + iv + CalcEVs( HP_EVs )  ) * Level / 100f + Level ) + 10 ) },
            { Stat.Attack,    Mathf.FloorToInt( ( ( ( 2 * PokeSO.Attack    + iv + CalcEVs( ATK_EVs )   ) * Level / 100f ) + 5 ) * GetNatureModifier( Stat.Attack ) ) },
            { Stat.Defense,   Mathf.FloorToInt( ( ( ( 2 * PokeSO.Defense   + iv + CalcEVs( DEF_EVs )   ) * Level / 100f ) + 5 ) * GetNatureModifier( Stat.Defense ) ) },
            { Stat.SpAttack,  Mathf.FloorToInt( ( ( ( 2 * PokeSO.SpAttack  + iv + CalcEVs( SPATK_EVs ) ) * Level / 100f ) + 5 ) * GetNatureModifier( Stat.SpAttack ) ) },
            { Stat.SpDefense, Mathf.FloorToInt( ( ( ( 2 * PokeSO.SpDefense + iv + CalcEVs( SPDEF_EVs ) ) * Level / 100f ) + 5 ) * GetNatureModifier( Stat.SpDefense ) ) },
            { Stat.Speed,     Mathf.FloorToInt( ( ( ( 2 * PokeSO.Speed     + iv + CalcEVs( SPE_EVs )   ) * Level / 100f ) + 5 ) * GetNatureModifier( Stat.Speed ) ) },
        };
    }

    private void InitializeEVs(){
        EffortValues = new()
        {
            { Stat.HP,          _hpEVs },
            { Stat.Attack,      _atkEVs },
            { Stat.Defense,     _defEVs },
            { Stat.SpAttack,    _spatkEVs },
            { Stat.SpDefense,   _spdefEVs },
            { Stat.Speed,       _speEVs },
        };
    }

    private float CalcEVs( int statEVs ){
        float ev = statEVs;
        float value = ev / 4;

        return Mathf.FloorToInt( value );
    }

    private int GetEVs( Stat stat ){
        return EffortValues[stat];
    }

    public void AssignEVs( Stat stat, int amount, bool loading = false )
    {
        if( !EffortValues.ContainsKey( stat ) )
            return;

        if( loading )
        {
            EffortValues[stat] = Mathf.Clamp( EffortValues[stat] + amount, 0, 252 );
            return;
        }

        if( EffortValues[stat] >= 252 )
        {
            DialogueManager.Instance.PlaySystemMessage( $"{_nickName}'s {stat} is maxed out!" );
            return;
        }

        int currentEVs = EffortValues[stat];
        
        if( amount > 0 )
        {
            //--Remove from pool, add to Stat
            int maxAdd = 252 - currentEVs;
            int add = Mathf.Min( amount, maxAdd, RemainingEffortPoints );

            EffortValues[stat] += add;
            RemainingEffortPoints -= add;
        }
        else if( amount < 0 )
        {
            //--Remove from stat, add to pool
            int maxReturn = Mathf.Min( Mathf.Abs( amount ), currentEVs );
            
            EffortValues[stat] -= maxReturn;
            RemainingEffortPoints += maxReturn;
        }

        RemainingEffortPoints = Mathf.Clamp( RemainingEffortPoints, 0, _gainedEffortPoints );
    }

    private void InitializeNatures()
    {
        Natures = new()
        {
            { NatureID.Neutral, new() },
            { NatureID.Lonely,      new(){ PositiveStat = Stat.Attack,      NegativeStat = Stat.Defense } },
            { NatureID.Brave,       new(){ PositiveStat = Stat.Attack,      NegativeStat = Stat.Speed } },
            { NatureID.Adamant,     new(){ PositiveStat = Stat.Attack,      NegativeStat = Stat.SpAttack } },
            { NatureID.Naughty,     new(){ PositiveStat = Stat.Attack,      NegativeStat = Stat.SpDefense } },
            { NatureID.Bold,        new(){ PositiveStat = Stat.Defense,     NegativeStat = Stat.Attack } },
            { NatureID.Relaxed,     new(){ PositiveStat = Stat.Defense,     NegativeStat = Stat.Speed } },
            { NatureID.Impish,      new(){ PositiveStat = Stat.Defense,     NegativeStat = Stat.SpAttack } },
            { NatureID.Lax,         new(){ PositiveStat = Stat.Defense,     NegativeStat = Stat.SpDefense } },
            { NatureID.Timid,       new(){ PositiveStat = Stat.Speed,       NegativeStat = Stat.Defense } },
            { NatureID.Hasty,       new(){ PositiveStat = Stat.Speed,       NegativeStat = Stat.Defense } },
            { NatureID.Jolly,       new(){ PositiveStat = Stat.Speed,       NegativeStat = Stat.SpAttack } },
            { NatureID.Naive,       new(){ PositiveStat = Stat.Speed,       NegativeStat = Stat.SpDefense } },
            { NatureID.Modest,      new(){ PositiveStat = Stat.SpAttack,    NegativeStat = Stat.Attack } },
            { NatureID.Mild,        new(){ PositiveStat = Stat.SpAttack,    NegativeStat = Stat.Defense } },
            { NatureID.Quiet,       new(){ PositiveStat = Stat.SpAttack,    NegativeStat = Stat.Speed } },
            { NatureID.Rash,        new(){ PositiveStat = Stat.SpAttack,    NegativeStat = Stat.SpDefense } },
            { NatureID.Calm,        new(){ PositiveStat = Stat.SpDefense,   NegativeStat = Stat.Attack } },
            { NatureID.Gentle,      new(){ PositiveStat = Stat.SpDefense,   NegativeStat = Stat.Defense } },
            { NatureID.Sassy,       new(){ PositiveStat = Stat.SpDefense,   NegativeStat = Stat.Speed } },
            { NatureID.Careful,     new(){ PositiveStat = Stat.SpDefense,   NegativeStat = Stat.SpAttack } },
        };
    }

    private float GetNatureModifier( Stat stat )
    {
        var nature = Natures[_currentNature];

        if( stat == nature.PositiveStat )
        {
            Debug.Log( $"[Pokemon][Nature] Calculating {NickName}'s {stat}! Their Nature is: {_currentNature}, giving their {stat} a 1.1f modifier!");
            return 1.1f;
        }
        else if( stat == nature.NegativeStat )
        {
            Debug.Log( $"[Pokemon][Nature] Calculating {NickName}'s {stat}! Their Nature is: {_currentNature}, giving their {stat} a 0.9f modifier!");
            return 0.9f;
        }
        else
        {
            Debug.Log( $"[Pokemon][Nature] Calculating {NickName}'s {stat}! Their Nature is: {_currentNature}, giving their {stat} a 1f modifier!");
            return 1f;
        }
    }

    private int GetStat( Stat stat )
    {
        float statValue = Stats[stat];

        int stage = StatStages[stat];
        var stageModifier = new float[] { 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f };
        var directModifier = DirectStatModifiers[stat].Values.Aggregate( 1.0f, ( acc, dsm ) => acc * dsm );

        if( stage >= 0 )
            statValue *= stageModifier[stage];
        else
            statValue /= stageModifier[-stage];

        //--Apply Direct Stat Change (Burn, Paralysis, Ruin Ability, Weather stat change, etc.)
        statValue *= directModifier;

        int final = Mathf.FloorToInt( statValue );

        return final;
    }

    public void ApplyStatStageChange( List<StatStage> statStages, StageChangeSource source )
    {
        for( int i = 0; i < statStages.Count; i++ )
            Debug.Log( $"Stat Stage Changed! Stat effected: {statStages[i].Stat}, Changed by: {statStages[i].Change}" );
        //--We convert our stat stages to a dictionary because the AbilityDB action takes in a dictionary that lets us remove a boost from it.
        //--We then use the modified dictionary, by looping through and providing the appropriate change in stat stages, which should never alter
        //--an ability-blocked stat. Abilities that utilize OnStatStageChange have their ability "activated" whenever, where ever we call ApplyStatChange() on a pokemon that has an appropriate ability.
        var stageDictionary = statStages.ToDictionary( x => x.Stat, x => x.Change );
        Ability?.OnStatStageChange?.Invoke( stageDictionary, source.Pokemon, this );

        if( MoveConditionDB.Conditions.ContainsKey( source.MoveName ) )
            MoveConditionDB.Conditions[source.MoveName]?.OnStatStageChange?.Invoke( stageDictionary, source.Pokemon, this );

        foreach( var kvp in stageDictionary )
        {
            var stat = kvp.Key;
            var change = kvp.Value;

            // Debug.Log( $"{NickName}'s {stat} was {GetStat( stat )}!" );
            StatStages[stat] = Mathf.Clamp( StatStages[stat] + change, -6, 6 );
            // Debug.Log( $"{NickName}'s {stat} is now {GetStat( stat )}!" );

            if( change == 1 )
                AddStatusEvent( StatusEventType.StatChange, $"{NickName}'s {stat} rose by {Mathf.Abs( change )} stage!", change );
            if( change > 1 && change < 6 )
                AddStatusEvent( StatusEventType.StatChange, $"{NickName}'s {stat} sharply rose by {Mathf.Abs( change )} stages!", change );
            if( change == 6 )
                AddStatusEvent( StatusEventType.StatChange, $"{NickName}'s {stat} has maxed out!", change );
            if( change == -1 )
                AddStatusEvent( StatusEventType.StatChange, $"{NickName}'s {stat} decreased by {Mathf.Abs( change )} stage!", change );
            if( change < -1 && change > -6 )
                AddStatusEvent( StatusEventType.StatChange, $"{NickName}'s {stat} sharply decreased by {Mathf.Abs( change )} stages!", change );
            if( change == -6 )
                AddStatusEvent( StatusEventType.StatChange, $"{NickName}'s {stat} has bottomed out!", change );

            if( BattleSystem.Instance != null )
            {
                if( change > 0 )
                {
                    var unit = BattleSystem.Instance.GetPokemonBattleUnit( this );
                    unit.SetFlagActive( UnitFlags.IncreasedStatStage, true );
                }
                else if( change < 0 )
                {
                    var unit = BattleSystem.Instance.GetPokemonBattleUnit( this );
                    unit.SetFlagActive( UnitFlags.LoweredStatStage, true );
                }
            }
        }

        Ability?.OnAfterStatStageChange?.Invoke( stageDictionary, source.Pokemon, this );
    }

    public List<StatStage> GetStatStages()
    {
        List<StatStage> changes = new();

        foreach( var kvp in StatStages )
        {
            StatStage ss = new() { Stat = kvp.Key, Change = kvp.Value };
            changes.Add( ss );
        }

        return changes;
    }

    public void ApplyDirectStatModifier( Stat stat, DirectModifierCause cause, float modifier )
    {
        Debug.Log( $"{NickName} received a Direct Stat Modifier {cause}: {stat} x {modifier}" );
        if( !DirectStatModifiers.TryGetValue( stat, out var statDict ) )
        {
            statDict = new();
            DirectStatModifiers[stat] = statDict;
        }

        statDict[cause] = modifier;
    }

    //--When we remove direct stat changes, we actually need to remove the value that was
    //--added to the list of modifiers for that stat, because those modifiers are
    //--multipled together to get the total modifier that gets multiplied to the stat (before stat stages)
    public void RemoveDirectStatModifier( Stat stat, DirectModifierCause cause ){ 
        Debug.Log( $"{NickName} is trying to Remove a Direct Stat Modifier for: {cause}, {stat}" );

        if( !DirectStatModifiers.TryGetValue( stat, out var statDict ) )
            return;

        if( cause == DirectModifierCause.Unmodified )
            return; //--Never remove base value so we never multiply a stat by 0. Base value is 1f

        if( statDict.ContainsKey( cause ) )
        {
            Debug.Log( $"{NickName} Removed a Direct Stat Modifier {cause}: {stat} x {statDict[cause]}" );
            statDict.Remove( cause );
        }
    }

    public void ResetStatChanges()
    {
        StatStages = new Dictionary<Stat, int>()
        {
            { Stat.HP,          0 },
            { Stat.Attack,      0 },
            { Stat.Defense,     0 },
            { Stat.SpAttack,    0 },
            { Stat.SpDefense,   0 },
            { Stat.Speed,       0 },
            { Stat.Accuracy,    0 },
            { Stat.Evasion,     0 },
        };

        DirectStatModifiers = new Dictionary<Stat, Dictionary<DirectModifierCause, float>>();
        foreach( Stat stat in Enum.GetValues( typeof( Stat ) ) )
        {
            DirectStatModifiers[stat] = new()
            {
              { DirectModifierCause.Unmodified, 1f }  
            };
        }
    }

    //--The way these work is they're simply being called during the attack phase.
    //--For Atk, SpAtk, Spd, Acc, we get passed the defending pokemon from BattleUnit.TakeDamage(), because the attacking pokemon is "this" pokemon instance
    //--For Def and SpDef, and probably EVA, the defending pokemon is "this" pokemon instance, and instead we get passed the attacker from BattleUnit.TakeDamage().
    public float Modify_ATK( float atk, Pokemon defender, Move move )
    {
        if( Ability?.OnModify_ATK != null )
            return Ability.OnModify_ATK( atk, this, defender, move );

        return atk;
    }
    
    public float Modify_SpATK( float spAtk, Pokemon defender, Move move )
    {
        if( Ability?.OnModify_SpATK != null )
            return Ability.OnModify_SpATK( spAtk, this, defender, move );

        return spAtk;
    }

    public float Modify_DEF( float def, Pokemon attacker, Move move )
    {
        if( Ability?.OnModify_DEF != null )
            return Ability.OnModify_DEF( def, attacker, this, move );

        return def;
    }

    public float Modify_SpDEF( float spDef, Pokemon attacker, Move move )
    {
        if( Ability?.OnModify_SpDEF != null )
            return Ability.OnModify_SpDEF( spDef, attacker, this, move );

        return spDef;
    }

    public float Modify_SPD( float spd, Pokemon defender, Move move )
    {
        if( Ability?.OnModify_SPD != null )
            return Ability.OnModify_SPD( spd, this, defender, move );
        
        return spd;
    }

    public float Modify_ACC( float acc, Pokemon defender, Move move )
    {
        if( Ability?.OnModify_ACC != null )
            return Ability.OnModify_ACC( acc, this, defender, move );

        return acc;
    }

    public float Modify_EVA( float eva, Pokemon attacker, Move move )
    {
        if( Ability?.OnModify_EVA != null )
            return Ability.OnModify_EVA( eva, attacker, this, move );

        return eva;
    }

    public void IncreaseHP( int amount ){
        CurrentHP = Mathf.Clamp( CurrentHP + amount, 0, MaxHP );
        Debug.Log( $"{NickName}'s current hp is now: {CurrentHP}" );
        OnDisplayInfoChanged?.Invoke();
    }

    public void DecreaseHP( int damage ){
        CurrentHP = Mathf.Clamp( CurrentHP - damage, 0, MaxHP );
        OnDisplayInfoChanged?.Invoke();
    }

    public void SetHP( int value )
    {
        CurrentHP = Mathf.Clamp( value, 0, MaxHP );
        OnDisplayInfoChanged?.Invoke();
    }

    private void UpdateHPOnLevelup( int previousMaxHP ){
        bool isFainted = false;
        if( CurrentHP == 0 )
            isFainted = true;

        float damageTaken = previousMaxHP - CurrentHP;
        int newCurrentHP = Mathf.FloorToInt( MaxHP - damageTaken );

        if( isFainted )
            CureSevereStatus();

        CurrentHP = newCurrentHP;
    }

    public void SetSevereStatus( SevereConditionID id, StatusEffectSource source )
    {
        //--This lets Faint override a previous status, since statuses can't replace each other normally. //--Faint is now handled directly in SetFainted() --02/11/26
        // if( SevereStatus != null && id == SevereConditionID.FNT )
        // {
        //     SevereStatus = SevereConditionsDB.Conditions[id];
        // }
        if( SevereStatus != null )
            return;

        bool canApply = CanApplySevereStatus( id, source );

        if( !canApply )
            return;

        SevereStatus = SevereConditionsDB.Conditions[id];

        SevereStatus?.OnApplyStatus?.Invoke( this );
        SevereStatus?.OnStart?.Invoke( this );
        AddStatusEvent( $"{_pokeSO.Species} {SevereStatus.StartMessage}" );

        Ability?.OnSetSevereStatus?.Invoke( id, this, source );

        OnStatusChanged?.Invoke();
    }

    public void SyncSevereStatus( SevereConditionID id )
    {
        SevereStatus = SevereConditionsDB.Conditions[id];
    }

    private bool CanApplySevereStatus( SevereConditionID id, StatusEffectSource source )
    {
        bool canApply = Ability?.OnTrySetSevereStatus?.Invoke( id, this, source ) ?? true;
        if( !canApply )
            return false;

        if( id == SevereConditionID.BRN && CheckTypes( PokemonType.Fire ) )
            return false;

        if( id == SevereConditionID.PAR && CheckTypes( PokemonType.Electric ) )
            return false;

        if( id == SevereConditionID.FBT && CheckTypes( PokemonType.Ice ) )
            return false;

        if( ( id == SevereConditionID.PSN || id == SevereConditionID.TOX ) && CheckTypes( PokemonType.Steel ) )
            return false;

        //--Safeguard
        if( BattleSystem.Instance != null )
        {
            var bs = BattleSystem.Instance;
            var unit = bs.GetPokemonBattleUnit( this );
            var court = bs.Field.GetUnitCourt( unit );

            if( court.Conditions.ContainsKey( CourtConditionID.SafeGuard ) && source.Pokemon != this && source.Pokemon.AbilityID != AbilityID.Infiltrator )
            {
                if( source.Source == EffectSource.Ability )
                    return false;

                if( source.Source == EffectSource.Move )
                    return false;
            }
        }

        return true;
    }

    public void CureSevereStatus()
    {
        // if( SevereStatus != null && SevereStatus.ID == SevereConditionID.BRN )
            // RemoveDirectStatModifier( Stat.Attack, DirectModifierCause.BRN );

        // if( SevereStatus != null && SevereStatus.ID == SevereConditionID.FBT )
            // RemoveDirectStatModifier( Stat.SpAttack, DirectModifierCause.FBT );

        if( SevereStatus != null && SevereStatus.ID == SevereConditionID.PAR )
            RemoveDirectStatModifier( Stat.Speed, DirectModifierCause.PAR );

        SevereStatus = null;
        OnStatusChanged?.Invoke(); //--For now this just sets the severe status icon in the battlehud
    }

    public void SetVolatileStatus( VolatileConditionID id, StatusEffectSource source, int duration = -1 ) //--consider adding the attacker or something as well
    {
        Debug.Log( $"[Volatile Status] Trying to set Volatile Status {id} on {NickName}!" );
        if( VolatileStatuses == null )
            VolatileStatuses = new();

        bool canApply = CanApplyVolatileStatus( id, source );

        if( !canApply )
            return;

        Debug.Log( $"[Volatile Status] Can be applied! Checking if it's not already in the list..." );
        var condition = VolatileConditionsDB.Conditions[id];

        Debug.Log( $"[Volatile Status] Adding {id} to {NickName}'s Volatile Statuses!" );
        VolatileStatuses.Add( id, ( condition, condition.Duration ) );

        condition?.OnStart?.Invoke( this );
        Ability?.OnSetVolatileStatus?.Invoke( id, this, source );

        AddStatusEvent( $"{_pokeSO.Species} {condition.StartMessage}" );
    }

    private bool CanApplyVolatileStatus( VolatileConditionID id, StatusEffectSource source )
    {
        if( VolatileStatuses.ContainsKey( id ) )
            return false;

        bool ability = Ability?.OnTrySetVolatileStatus?.Invoke( id, this, source ) ?? true;
        if( !ability )
            return false;

        if( id == VolatileConditionID.Infatuation )
        {
            if( _gender == source.Pokemon.Gender)
                return false;
        }

        //--Safeguard
        if( id == VolatileConditionID.Confusion || id == VolatileConditionID.Yawn )
        {
            if( BattleSystem.Instance != null )
            {
                var bs = BattleSystem.Instance;
                var unit = bs.GetPokemonBattleUnit( this );
                var court = bs.Field.GetUnitCourt( unit );

                if( court.Conditions.ContainsKey( CourtConditionID.SafeGuard ) && source.Pokemon != this && source.Pokemon.AbilityID != AbilityID.Infiltrator )
                {
                    if( source.Source == EffectSource.Ability )
                        return false;

                    if( source.Source == EffectSource.Move )
                        return false;
                }
            }
        }

        return true;
    }

    public void CureVolatileStatus( VolatileConditionID id )
    {
        if( VolatileStatuses.ContainsKey( id ) )
            VolatileStatuses.Remove( id );
    }

    public void SetVolatileStatusTime( VolatileConditionID id, int duration )
    {
        if( !VolatileStatuses.TryGetValue( id, out var status ) )
            return;

        status.Duration = duration;
        VolatileStatuses[id] = status;
    }

    public Dictionary<VolatileConditionID, ( VolatileCondition Condition, int duration )> GetBatonPassStatuses()
    {
        Dictionary<VolatileConditionID, ( VolatileCondition Condition, int duration )> passStatuses = new();

        foreach( var kvp in VolatileStatuses )
        {
            if( kvp.Value.Condition.Passable )
                passStatuses.Add( kvp.Key, kvp.Value );
            else
                continue;
        }

        return passStatuses;
    }

    public void ClearAllVolatileStatus()
    {
        // VolatileStatus.Clear();
        VolatileStatuses.Clear();
    }

    public void SetTransientStatus( TransientConditionID id, StatusEffectSource source )
    {
        if( !CanApplyTransientStatus( id ) )
            return;
            
        Debug.Log( $"SetTransientStatus()" );
        //--May need to limit to one transient status at a time
        TransientStatus = TransientConditionsDB.Conditions[id];
        TransientStatusActive = true;
        TransientStatus?.OnStart?.Invoke( this );

        if( TransientStatus != null )
        {
            Debug.Log( $"{NickName} was Transiently affected by {TransientStatus.Name}! TransientStatusActive is: {TransientStatusActive}" );
        }

        Ability?.OnSetTransientStatus?.Invoke( id, this, source );
    }

    private bool CanApplyTransientStatus( TransientConditionID id )
    {
        if( id == TransientConditionID.Flinch && AbilityID == AbilityID.InnerFocus )
            return false;
        else
            return true;
    }

    public void CureTransientStatus()
    {
        if( TransientStatus != null )
            TransientStatus = null;

        TransientStatus?.OnExit?.Invoke( this );
        TransientStatusActive = false;
    }

    public void SetBindingStatus( BindingConditionID id, StatusEffectSource source )
    {
        Debug.Log( $"Setting a binding status!" );
        if( BindingStatuses == null )
            BindingStatuses = new();

        var status = BindingConditionsDB.Conditions[id];
        if( BindingStatuses.ContainsKey( id ) )
            return;

        BindingStatuses.Add( id, ( status, status.Duration ) );
        
        status?.OnStart?.Invoke( this );
        Ability?.OnSetBindingStatus?.Invoke( id, this, source );

        AddStatusEvent( $"{_pokeSO.Species} {status.StartMessage}" );
    }

    public void CureBindingStatus()
    {
        if( BindingStatuses != null )
            BindingStatuses = null;
    }

    public Move GetRandomMove()
    {
        int r = UnityEngine.Random.Range( 0, ActiveMoves.Count );
        return ActiveMoves[r];
    }

    public Move GetRandomMoveExcluding( Move move )
    {
        List<Move> selection = new();
        for( int i = 0; i < _activeMoves.Count; i++ )
        {
            if( _activeMoves[i] == move )
                continue;
            else
                selection.Add( _activeMoves[i] );
        }

        int r = UnityEngine.Random.Range( 0, selection.Count );
        return selection[r];
    }

    public void OnApplyStatus(){
        SevereStatus?.OnApplyStatus?.Invoke( this );
    }

    public bool OnBeforeTurn_Severe(){
        // Debug.Log( $"Severe Status is: {SevereStatus?.ID}, and has: {SevereStatusTime} turns left" );
        if( SevereStatus?.OnBeforeTurn != null )
            return SevereStatus.OnBeforeTurn( this );

        return true;
    }

    public bool OnBeforeTurn_Volatile()
    {
        //--Volatile Status
        if( VolatileStatuses != null && VolatileStatuses.Count > 0 )
        {
            foreach( var kvp in VolatileStatuses )
            {
                bool success = kvp.Value.Condition?.OnBeforeTurn?.Invoke( this ) ?? true;
                if( !success )
                    return false;
                else
                    continue;
            }
        }

        return true;
    }

    public bool OnBeforeTurn_Transient()
    {
        //--Transient Status
        if( TransientStatus?.OnBeforeTurn != null )
            return TransientStatus.OnBeforeTurn( this );
        else
            return true;
    }

    public void OnBattleEnded()
    {
        ClearAllVolatileStatus();
        CureTransientStatus();
        CureBindingStatus();
        ResetStatChanges();
        CalculateStats();
    }

    public void FullHeal()
    {
        CureSevereStatus();
        ClearAllVolatileStatus();
        CureTransientStatus();
        CureBindingStatus();
        ResetStatChanges();
        CalculateStats();
        CurrentHP = MaxHP;
    }

    public void StatUpdated()
    {
        CalculateStats();
    }

    public void SetFainted()
    {
        SevereStatus = SevereConditionsDB.Conditions[SevereConditionID.FNT];
        CurrentHP = 0;
    }

    public bool IsFainted()
    {
        return CurrentHP <= 0 || SevereStatus?.ID == SevereConditionID.FNT;
    }

    public void AddStatusEvent( StatusEventType type, string message, int change = 0 )
    {
        StatusChanges.Enqueue( new( type, message, change ) );
    }

    public void AddStatusEvent( string message, int change = 0 )
    {
        Debug.Log( $"[Status Event] Adding status event string {message}" );
        StatusChanges.Enqueue( new( StatusEventType.Text, message, change ) );
    }

    private string GeneratePID( int hpIV, int atkIV, int defIV, int spatkIV, int spdefIV, int speIV, int natureID, int abilityIndex, int formIndex )
    {
        int gender;

        if( Gender == Gender.Male )
            gender = 0;
        else if( Gender == Gender.Female )
            gender = 1;
        else
            gender = 2;

        int trainerID = UnityEngine.Random.Range( 1, 9999 );
        int dexNO = PokeSO.DexNO;

        string pidString =
        $"{gender:D2}" +
        $"{hpIV:D2}" +
        $"{atkIV:D2}" +
        $"{defIV:D2}" +
        $"{spatkIV:D2}" +
        $"{spdefIV:D2}" +
        $"{speIV:D2}" +
        $"{natureID:D2}" +
        $"{abilityIndex:D2}" +
        $"{formIndex:D2}" +
        $"{trainerID:D4}" +
        $"{dexNO:D3}";

        Debug.Log( $"{NickName}'s PID: {pidString}" );
        return pidString;
    }

    public void CloneOverridePID( string id )
    {
        _pid = id;
    }

}

public enum Gender { Male, Female, None, }
public enum NatureID { None, Neutral, Lonely, Brave, Adamant, Naughty, Bold, Relaxed, Impish, Lax, Timid, Hasty, Jolly, Naive, Modest, Mild, Quiet, Rash, Calm, Gentle, Sassy, Careful }
public class Nature
{
    public Stat PositiveStat { get; set; }
    public Stat NegativeStat { get; set; }
}

public enum DirectModifierCause
{
    Unmodified,
    BRN, FBT, PAR,
    WeatherDEF, WeatherSpDEF, WeatherSPD,
    Tailwind, Reflect, LightScreen,
    ChoiceBand, ChoiceSpecs, ChoiceScarf,
    LightBall, Guts, MarvelScale,
    SolarPower, SandVeil, Hustle,

}

public class DamageDetails
{
    public int DamageDealt { get; set ; }
    public bool Fainted { get; set; }
    public float Critical { get; set; }
    public float TypeEffectiveness { get; set; }
}

[Serializable]
public class PokemonSaveData
{
    public string PID;
    public string Species;
    public int Form;
    public string NickName;
    public int Level;
    public NatureID CurrentNature;
    public NatureID DefaultNature;
    public int CurrentAbilityIndex;
    public string HeldItem;
    public int Friendship;
    public bool CanEvolveByLevelUp;
    public PokeBallType CurrentBall;
    public int CurrentHP;
    public int Exp;
    public int GainedEffortPoints;
    public int RemainingEffortPoints;
    public int HP_EVs;
    public int ATK_EVs;
    public int DEF_EVs;
    public int SPATK_EVs;
    public int SPDEF_EVs;
    public int SPE_EVs;
    public SevereConditionID? SevereStatus;
    public List<MoveSaveData> ActiveMoves;
    public List<MoveSaveData> LearnedMoves;
}

public enum StatusEventType { Text, Damage, StatChange, SevereStatusDamage, VolatileStatusDamage, SevereStatusPassive, VolatileStatusPassive, AbilityCutIn, Heal }
public class StatusEvent
{
    public StatusEventType Type { get; private set; }
    public string Message { get; private set; }
    public int StageChange { get; private set; }

    public StatusEvent( StatusEventType type, string message, int change )
    {
        Type = type;
        Message = message;
        StageChange = change;
    }
}

public enum StageChangeSourceType { Move, Ability, Weather, Court }
public class StageChangeSource
{
    public Pokemon Pokemon { get; set; }
    public string MoveName { get; set; }
    public StageChangeSourceType Source { get; set; }
}

public enum EffectSource { Move, Ability, Item, Court, Phazed, Drowsy }
public class StatusEffectSource
{
    public Pokemon Pokemon { get; set; }
    public EffectSource Source { get; set; }
}
