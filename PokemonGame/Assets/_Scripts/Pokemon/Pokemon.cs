using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEditor.SceneManagement;
using System.Resources;

[Serializable]
public class Pokemon
{
    [SerializeField] private PokemonSO _pokeSO;
    [SerializeField] private string _nickName;  
    [SerializeField] private int _level;
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
    public PokemonSO PokeSO => _pokeSO;
    public int Level => _level;
    public string NickName => _nickName;
    public Ability Ability => _ability;
    public int CurrentAbilityIndex => _currentAbilityIndex; //--Might change these to a singular AbilityID instead of playing around with indicies, let's see how the rest of the implementation goes...
    public ItemSO HeldItem => _heldItem;
    public int Exp { get; private set; }
    public BattleItemEffect BattleItemEffect { get; set; }
    public bool CanEvolveByLevelUp { get; private set; }
    public bool IsPlayerUnit { get; private set; }
    public bool IsEnemyUnit { get; private set; }
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
    public Dictionary<Stat, int> StatStage { get; private set; }
    public Dictionary<Stat, Dictionary<DirectModifierCause, float>> DirectStatModifiers { get; private set; }
    public StatusCondition SevereStatus { get; private set; }
    public StatusCondition VolatileStatus { get; private set; }
    public StatusCondition TransientStatus { get; private set; }
    public int SevereStatusTime { get; set; }
    public int VolatileStatusTime { get; set; }
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

    public Pokemon( PokemonSO pokeSO, int level ){
        _pokeSO = pokeSO;
        _level = level;

        Init();
    }

    public Pokemon( PokemonSaveData saveData ){
        _pokeSO                 = PokemonDB.GetPokemonBySpecies( ( saveData.Species, saveData.Form ) );
        _nickName               = saveData.NickName;
        _level                  = saveData.Level;
        _currentAbilityIndex    = saveData.CurrentAbilityIndex;
        _heldItem               = ItemsDB.GetItemByName( saveData.HeldItem );
        CanEvolveByLevelUp      = saveData.CanEvolveByLevelUp;
        CurrentHP               = saveData.CurrentHP;
        Exp                     = saveData.Exp;
        _gainedEffortPoints     = saveData.GainedEffortPoints;
        RemainingEffortPoints   = saveData.RemainingEffortPoints;
        _currentBallType        = saveData.CurrentBall;
        IsPlayerUnit            = saveData.IsPlayerUnit;

        InitializeEVs();
        AssignEVs( Stat.HP,         saveData.HP_EVs, true );
        AssignEVs( Stat.Attack,     saveData.ATK_EVs, true );
        AssignEVs( Stat.Defense,    saveData.DEF_EVs, true );
        AssignEVs( Stat.SpAttack,   saveData.SPATK_EVs, true );
        AssignEVs( Stat.SpDefense,  saveData.SPDEF_EVs, true );
        AssignEVs( Stat.Speed,      saveData.SPE_EVs, true );

        GetCurrentAbility();
        SetupBattleItem();

        if( saveData.SevereStatus != null ){
            SevereStatus = StatusConditionsDB.Conditions[saveData.SevereStatus.Value];
            OnStatusChanged?.Invoke();
        }
        else
            SevereStatus = null;

        //--Restore Moves
        _activeMoves = saveData.ActiveMoves.Select( s => new Move( s ) ).ToList(); //--Active Moves
        _learnedMoves = saveData.LearnedMoves.Select( s => new Move( s ) ).ToList(); //--Full list of known moves, not including active moves. TODO

        CalculateStats();

        ResetStatChanges();
        VolatileStatus = null;
        TransientStatus = null;

        StatusChanges = new Queue<StatusEvent>();
    }

//--------------------------------------------------------------------------------------------
//-------------------------------------[ FUNCTIONS ]------------------------------------------
//--------------------------------------------------------------------------------------------
    
    public void Init(){
        //--Set Name
        _nickName = PokeSO.Species;
        GetCurrentAbility();
        SetupBattleItem();

        //--------GENERATE MOVES-----------
        bool populateActiveMoves = true;
        if( _preSelectedMoves != null && _preSelectedMoves.Count > 0 )
            populateActiveMoves = false;

        _activeMoves = new List<Move>();
        _learnedMoves = new List<Move>();
        foreach( var move in PokeSO.LearnableMoves ){
            //--Add moves from the most recent down, i think until we reach max active moves
            //--once we reach max active moves, if there's still more moves we should learn
            //--from our learnset via levelup, add them to the LearnedMoves list for access later
            if( move.LevelLearned <= Level && ActiveMoves.Count < PokemonSO.MAX_ACTIVE_MOVES && populateActiveMoves ){
                ActiveMoves.Add( new Move( move.MoveSO ) );
            }
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
        _gainedEffortPoints = 20;
        RemainingEffortPoints = 20;
        InitializeEVs();
        CalculateStats();
        ResetStatChanges();
        CurrentHP = MaxHP;

        //--Status
        SevereStatus = null;
        VolatileStatus = null;
        TransientStatus = null;

        StatusChanges = new Queue<StatusEvent>();

        if( _currentBallType == PokeBallType.None )
            _currentBallType = PokeBallType.PokeBall;
        
        //--Events
        BattleSystem.OnBattleEnded += OnBattleEnded;
    }

    public PokemonSaveData CreateSaveData(){
        var saveData = new PokemonSaveData(){
            Species = _pokeSO.Species,
            Form = _pokeSO.Form,
            NickName = _nickName,
            Level = _level,
            CurrentAbilityIndex = _currentAbilityIndex,
            HeldItem = _heldItem.ItemName,
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
            IsPlayerUnit = IsPlayerUnit,
        };

        return saveData;
    }

    private void GetCurrentAbility()
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

    public void SetAsPlayerUnit(){
        IsPlayerUnit = true;
    }

    public void SetAsEnemyUnit(){
        IsEnemyUnit = true;
    }

    public void ChangeNickName( string name ){
        _nickName = name;
    }

    public void ChangeCurrentBall( PokeBallType ball ){
        _currentBallType = ball;
        OnDisplayInfoChanged?.Invoke();
    }

    public bool CheckTypes( PokemonType type ){
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
            _nickName = evo.Evolution.Species;

        int previousMaxHP = MaxHP;
        _pokeSO = evo.Evolution;
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

    public void LearnLevelUpMove( MoveSO newMove ){
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

    public void ReplaceWithNewMove( MoveSO replacedMove, MoveSO newMove ){
        for( int i = 0; i < ActiveMoves.Count; i++ ){
            if( ActiveMoves[i].MoveSO == replacedMove ){
                ActiveMoves[i] = new Move ( newMove );
                LearnedMoves.Add( new Move( replacedMove ) );
            }
        }
    }
    public bool CheckHasMove( MoveSO move ){
        return ActiveMoves.Count( m => m.MoveSO == move ) > 0 || _learnedMoves.Count( m=> m.MoveSO == move ) > 0;
    }

    public bool CheckHasMove( string move ){
        return ActiveMoves.Count( m => m.MoveSO.Name == move ) > 0 || _learnedMoves.Count( m=> m.MoveSO.Name == move ) > 0;
    }

    public bool CheckCanLearnMove( MoveSO move ){
        return PokeSO.TeachableMoves.Contains( move );
    }

    private void CalculateStats(){
        int iv = 31;
        int nature = 1;

        Stats = new()
        {
            { Stat.HP,        Mathf.FloorToInt( ( ( 2 * PokeSO.MaxHP     + iv + CalcEVs( HP_EVs )  ) * Level / 100f + Level ) + 10 ) },
            { Stat.Attack,    Mathf.FloorToInt( ( ( ( 2 * PokeSO.Attack    + iv + CalcEVs( ATK_EVs )   ) * Level / 100f ) + 5 ) * nature ) },
            { Stat.Defense,   Mathf.FloorToInt( ( ( ( 2 * PokeSO.Defense   + iv + CalcEVs( DEF_EVs )   ) * Level / 100f ) + 5 ) * nature ) },
            { Stat.SpAttack,  Mathf.FloorToInt( ( ( ( 2 * PokeSO.SpAttack  + iv + CalcEVs( SPATK_EVs ) ) * Level / 100f ) + 5 ) * nature ) },
            { Stat.SpDefense, Mathf.FloorToInt( ( ( ( 2 * PokeSO.SpDefense + iv + CalcEVs( SPDEF_EVs ) ) * Level / 100f ) + 5 ) * nature ) },
            { Stat.Speed,     Mathf.FloorToInt( ( ( ( 2 * PokeSO.Speed     + iv + CalcEVs( SPE_EVs )   ) * Level / 100f ) + 5 ) * nature ) },
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

    private int GetStat( Stat stat ){
        int statValue = Stats[stat];

        int boost = StatStage[stat];
        var changeModifier = new float[] { 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f };
        var directModifier = DirectStatModifiers[stat].Values.Aggregate( 1.0f, ( acc, dsm ) => acc * dsm );

        // Debug.Log( $"The Modifier value applied to {stat} is: {directModifier}" );

        //--Apply Direct Stat Change (Burn, Paralysis, Ruin Ability, Weather stat change, etc.)
        // Debug.Log( $"Stat Value before direct modifier: {statValue}" );
        statValue = Mathf.FloorToInt( statValue * directModifier );
        // Debug.Log( $"Stat Value after direct modifier: {statValue}" );

        if( boost >= 0 )
            statValue = Mathf.FloorToInt( statValue * changeModifier[boost] );
        else
            statValue = Mathf.FloorToInt( statValue / changeModifier[-boost] );

        return statValue;
    }

    public void ApplyStatStageChange( List<StatStage> statStages, Pokemon source ){
        for( int i = 0; i < statStages.Count; i++ )
            Debug.Log( $"Stat Stage Changed! Stat effected: {statStages[i].Stat}, Changed by: {statStages[i].Change}" );
        //--We convert our stat stages to a dictionary because the AbilityDB action takes in a dictionary that lets us remove a boost from it.
        //--We then use the modified dictionary, by looping through and providing the appropriate change in stat stages, which should never alter
        //--an ability-blocked stat. Abilities that utilize OnStatStageChange have their ability "activated" whenever, where ever we call ApplyStatChange() on a pokemon that has an appropriate ability.
        var stageDictionary = statStages.ToDictionary( x => x.Stat, x => x.Change );
        Ability?.OnStatStageChange?.Invoke( stageDictionary, source, this );
        // Debug.Log( $"{NickName}'s ability is: {Ability?.Name}. {source.NickName}'s ability is: {source.Ability?.Name}" );

        foreach( var kvp in stageDictionary )
        {
            var stat = kvp.Key;
            var change = kvp.Value;

            Debug.Log( $"{NickName}'s {stat} was {GetStat( stat )}!" );
            StatStage[stat] = Mathf.Clamp( StatStage[stat] + change, -6, 6 );
            Debug.Log( $"{NickName}'s {stat} is now {GetStat( stat )}!" );

            if( change == 1 )
                AddStatusEvent( StatusEventType.StatChange, $"{NickName}'s {stat} rose by {Mathf.Abs( change )} stage!" );
            if( change > 1 && change < 6 )
                AddStatusEvent( StatusEventType.StatChange, $"{NickName}'s {stat} sharply rose by {Mathf.Abs( change )} stages!" );
            if( change == 6 )
                AddStatusEvent( StatusEventType.StatChange, $"{NickName}'s {stat} has maxed out!" );
            if( change == -1 )
                AddStatusEvent( StatusEventType.StatChange, $"{NickName}'s {stat} decreased by {Mathf.Abs( change )} stage!" );
            if( change < -1 && change > -6 )
                AddStatusEvent( StatusEventType.StatChange, $"{NickName}'s {stat} sharply decreased by {Mathf.Abs( change )} stages!" );
            if( change == -6 )
                AddStatusEvent( StatusEventType.StatChange, $"{NickName}'s {stat} has bottomed out!" );
        }
    }

    public void ApplyDirectStatModifier( Stat stat, DirectModifierCause cause, float modifier ){
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

    private void ResetStatChanges(){
        StatStage = new Dictionary<Stat, int>()
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

    public void SetSevereStatus( StatusConditionID conditionID, EffectSource effectSource = EffectSource.Move ){
        //--This lets Faint override a previous status, since statuses can't replace each other normally.
        if( SevereStatus != null && conditionID == StatusConditionID.FNT ){
            SevereStatus = StatusConditionsDB.Conditions[conditionID];
        }
        else if( SevereStatus != null )
            return;

        bool canApply = Ability?.OnTrySetSevereStatus?.Invoke( conditionID, this, effectSource ) ?? true;

        if( !canApply )
            return;

        SevereStatus = StatusConditionsDB.Conditions[conditionID];

        if( BattleSystem.BattleIsActive ){
            SevereStatus?.OnApplyStatus?.Invoke( this );
            SevereStatus?.OnStart?.Invoke( this );
            AddStatusEvent( $"{_pokeSO.Species} {SevereStatus.StartMessage}" );
        }

        // Debug.Log( $"{_pokeSO.Species} {SevereStatus.StartMessage}" );
        OnStatusChanged?.Invoke();
    }

    public void CureSevereStatus(){
        if( SevereStatus != null && SevereStatus.ID == StatusConditionID.BRN )
            RemoveDirectStatModifier( Stat.Attack, DirectModifierCause.BRN );

        if( SevereStatus != null && SevereStatus.ID == StatusConditionID.FBT )
            RemoveDirectStatModifier( Stat.SpAttack, DirectModifierCause.FBT );

        if( SevereStatus != null && SevereStatus.ID == StatusConditionID.PAR )
            RemoveDirectStatModifier( Stat.Speed, DirectModifierCause.PAR );

        SevereStatus = null;
        OnStatusChanged?.Invoke(); //--For now this just sets the severe status icon in the battlehud
    }

    public void SetVolatileStatus( StatusConditionID conditionID, EffectSource effectSource = EffectSource.Move ){
        if( VolatileStatus != null )
            return;

        bool canApply = Ability?.OnTrySetVolatileStatus?.Invoke( conditionID, this, effectSource ) ?? true;

        if( !canApply )
            return;

        VolatileStatus = StatusConditionsDB.Conditions[conditionID];
        VolatileStatus?.OnStart?.Invoke( this );
        AddStatusEvent( $"{_pokeSO.Species} {VolatileStatus.StartMessage}" );
        // Debug.Log( $"{_pokeSO.Species} has been afflicted with: {ConditionsDB.Conditions[conditionID].Name}" );
        // OnStatusChanged?.Invoke(); -------will add some visual effect for volatile statuses eventually
    }

    public void CureVolatileStatus(){
        VolatileStatus = null;
        // OnStatusChanged?.Invoke(); -------will add some visual effect for volatile statuses eventually
    }

    public void SetTransientStatus( StatusConditionID id )
    {
        //--May need to limit to one transient status at a time
        TransientStatus = StatusConditionsDB.Conditions[id];
        TransientStatus?.OnStart?.Invoke( this );

        if( TransientStatus != null )
        {
            Debug.Log( $"{NickName} was Transiently affected by {TransientStatus.Name}! TransientStatusActive is: {TransientStatusActive}" );
        }
    }

    public void CureTransientStatus()
    {
        if( TransientStatus != null )
            TransientStatus = null;

        TransientStatusActive = false;
    }

    public Move GetRandomMove(){
        int r = UnityEngine.Random.Range( 0, ActiveMoves.Count );
        return ActiveMoves[r];
    }

    public void OnApplyStatus(){
        SevereStatus?.OnApplyStatus?.Invoke( this );
    }

    public bool OnBeforeTurn(){
        // Debug.Log( $"Severe Status is: {SevereStatus?.ID}, and has: {SevereStatusTime} turns left" );
        if( SevereStatus?.OnBeforeTurn != null )
            return SevereStatus.OnBeforeTurn( this );

        //--Volatile Status
        if( VolatileStatus?.OnBeforeTurn != null )
            return VolatileStatus.OnBeforeTurn( this );

        //--Transient Status
        if( TransientStatus?.OnBeforeTurn != null )
            return TransientStatus.OnBeforeTurn( this );

        return true;
    }

    public void OnAfterTurn(){
        SevereStatus?.OnAfterTurn?.Invoke( this );
        VolatileStatus?.OnAfterTurn?.Invoke( this );
        TransientStatus?.OnAfterTurn?.Invoke( this ); //--probably for enabling protect!
    }

    public void OnBattleEnded(){
        CureVolatileStatus();
        ResetStatChanges();
        CalculateStats();
    }

    public void FullHeal()
    {
        CureSevereStatus();
        CureVolatileStatus();
        ResetStatChanges();
        CalculateStats();
        CurrentHP = MaxHP;
    }

    public void StatUpdated()
    {
        CalculateStats();
    }

    public bool IsFainted()
    {
        return CurrentHP <= 0 || SevereStatus?.ID == StatusConditionID.FNT;
    }

    public void AddStatusEvent( StatusEventType type, string message )
    {
        StatusChanges.Enqueue( new( type, message ) );
    }

    public void AddStatusEvent( string message )
    {
        StatusChanges.Enqueue( new( StatusEventType.Text, message ) );
    }

}

public enum DirectModifierCause
{
    Unmodified,
    BRN, FBT, PAR,
    WeatherDEF, WeatherSpDEF, WeatherSPD,
    Tailwind, Reflect, LightScreen,
    ChoiceBand, ChoiceSpecs, ChoiceScarf,
    LightBall, Guts, MarvelScale,
    SolarPower,

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
    public string Species;
    public int Form;
    public string NickName;
    public int Level;
    public int CurrentAbilityIndex;
    public string HeldItem;
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
    public StatusConditionID? SevereStatus;
    public List<MoveSaveData> ActiveMoves;
    public List<MoveSaveData> LearnedMoves;
    public bool IsPlayerUnit;
}

public enum StatusEventType { Text, Damage, StatChange, SevereStatusDamage, VolatileStatusDamage, SevereStatusPassive, VolatileStatusPassive, AbilityCutIn }

public class StatusEvent
{
    public StatusEventType Type { get; private set; }
    public string Message { get; private set; }

    public StatusEvent( StatusEventType type, string message )
    {
        Type = type;
        Message = message;
    }
}
