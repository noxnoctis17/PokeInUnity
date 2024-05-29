using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

[Serializable]
public class Pokemon
{
    [SerializeField] private PokemonSO _pokeSO;
    [SerializeField] private int _level;
    public PokemonSO PokeSO => _pokeSO;
    public int Level => _level;
    public int Exp { get; private set; }
    public bool CanEvolveByLevelUp { get; private set; }
    public bool IsPlayerUnit { get; private set; }
    public bool IsEnemyUnit { get; private set; }
    public int CurrentHP { get; set; }
    public PokeBallType CurrentBallType;
    public Sprite CurrentBallSprite => PokeBallIconAtlas.PokeBallIcons[CurrentBallType];
    public List<Move> ActiveMoves { get; set; }
    public List<Move> LearnedMoves { get; set; }
    public Dictionary<Stat, int> Stats { get; private set; }
    public Dictionary<Stat, int> StatBoost { get; private set; }
    public Dictionary<Stat, List<float>> DirectStatChange { get; private set; }
    public Condition SevereStatus { get; private set; }
    public Condition VolatileStatus { get; private set; }
    public int SevereStatusTime { get; set; }
    public int VolatileStatusTime { get; set; }
    public Queue<string> StatusChanges { get; private set; }

//==================[ Events ]===========================================
    public event Action OnStatusChanged;
    public event Action OnDisplayInfoChanged;

//--------------------------------------------------------------------------------------------
//-----------------------------------[POKEMON STATS]------------------------------------------
//--------------------------------------------------------------------------------------------

    //--Stats
    public int MaxHP { get; private set; }
    public int Attack =>    GetStat( Stat.Attack );
    public int Defense =>   GetStat( Stat.Defense );
    public int SpAttack =>  GetStat( Stat.SpAttack );
    public int SpDefense => GetStat( Stat.SpDefense );
    public int Speed =>     GetStat( Stat.Speed );

    //--Direct Stat Modifier (burn, paralyze, ruin abilities, etc. )
    public float HP_Modifier { get; private set; }


//--------------------------------------------------------------------------------------------
//------------------------------------[EFFORT VALUES]-----------------------------------------
//--------------------------------------------------------------------------------------------
    public int GainedEffortPoints { get; private set; }
    public int RemainingEffortPoints { get; private set; }
    public int HP_EVs { get; private set; }
    public int ATK_EVs { get; private set; }
    public int DEF_EVs { get; private set; }
    public int SPATK_EVs { get; private set; }
    public int SPDEF_EVs { get; private set; }
    public int SPE_EVs { get; private set; }

//--------------------------------------------------------------------------------------------
//------------------------------------[ CONSTRUCTORS ]----------------------------------------
//--------------------------------------------------------------------------------------------

    public Pokemon( PokemonSO pokeSO, int level ){
        _pokeSO = pokeSO;
        _level = level;

        Init();
    }

    public Pokemon( PokemonSaveData saveData ){
        _pokeSO                 = PokemonDB.GetPokemonBySpecies( saveData.Species );
        _level                  = saveData.Level;
        CurrentHP               = saveData.CurrentHP;
        Exp                     = saveData.Exp;
        GainedEffortPoints      = saveData.GainedEffortPoints;
        CurrentBallType         = saveData.CurrentBall;
        HP_EVs                  = saveData.HP_EVs;
        ATK_EVs                 = saveData.ATK_EVs;
        DEF_EVs                 = saveData.DEF_EVs;
        SPATK_EVs               = saveData.SPATK_EVs;
        SPDEF_EVs               = saveData.SPDEF_EVs;
        SPE_EVs                 = saveData.SPE_EVs;
        IsPlayerUnit            = saveData.IsPlayerUnit;

        if( saveData.SevereStatus != null ){
            SevereStatus = ConditionsDB.Conditions[saveData.SevereStatus.Value];
            OnStatusChanged?.Invoke();
        }
        else
            SevereStatus = null;

        //--Restore Moves
        ActiveMoves = saveData.ActiveMoves.Select( s => new Move( s ) ).ToList(); //--Active Moves
        LearnedMoves = saveData.LearnedMoves.Select( s => new Move( s ) ).ToList(); //--Full list of known moves, not including active moves. TODO

        CalculateStats();

        ResetStatChanges();
        VolatileStatus = null;

        StatusChanges = new Queue<string>();
    }

//--------------------------------------------------------------------------------------------
//-------------------------------------[ FUNCTIONS ]------------------------------------------
//--------------------------------------------------------------------------------------------
    
    public void Init(){
        // Debug.Log( PokeSO.Name + ": called Init()" );
        //--------GENERATE MOVES-----------
        ActiveMoves = new List<Move>();
        LearnedMoves = new List<Move>();
        foreach( var move in PokeSO.LearnableMoves ){
            //--Add moves from the most recent down, i think until we reach max active moves
            //--once we reach max active moves, if there's still more moves we should learn
            //--from our learnset via levelup, add them to the LearnedMoves list for access later
            if( move.LevelLearned <= Level && ActiveMoves.Count < PokemonSO.MAX_ACTIVE_MOVES ){
                ActiveMoves.Add( new Move( move.MoveSO ) );
            }
            else if( ActiveMoves.Count == PokemonSO.MAX_ACTIVE_MOVES )
                LearnedMoves.Add( new Move( move.MoveSO ) );
        }

        // Debug.Log( "Exp Before setting in PokemonClass.Init(): " + Exp );
        if( Exp == 0 )
            Exp = PokeSO.GetExpForLevel( Level );
        // Debug.Log( "Exp After setting in PokemonClass.Init(): " + Exp );


        CalculateStats();
        CurrentHP = MaxHP;

        ResetStatChanges();
        SevereStatus = null;
        VolatileStatus = null;

        StatusChanges = new Queue<string>();

        CurrentBallType = PokeBallType.PokeBall;
        

        //--Events
        BattleSystem.OnBattleEnded += ResetStatChanges;
        BattleSystem.OnBattleEnded += CureVolatileStatus;
    }

    public PokemonSaveData CreateSaveData(){
        var saveData = new PokemonSaveData(){
            Species = _pokeSO.Species,
            Level = _level,
            CurrentHP = CurrentHP,
            Exp = Exp,
            GainedEffortPoints = GainedEffortPoints,
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

        // Debug.Log( $"{PokeSO.Name} has {ActiveMoves.Count} moves" );
        // Debug.Log( $"{PokeSO.Name}'s SaveData has {saveData.ActiveMoves.Count} moves" );

        return saveData;
    }

    public void SetAsPlayerUnit(){
        IsPlayerUnit = true;
    }

    public void SetAsEnemyUnit(){
        IsEnemyUnit = true;
    }

    public void ChangeCurrentBall( PokeBallType ball ){
        CurrentBallType = ball;
        OnDisplayInfoChanged?.Invoke();
    }

    public bool CheckTypes( PokemonType type ){
        if( PokeSO.Type1 == type || PokeSO.Type2 == type )
            return true;

        return false;
    }

    public void GainExp( int gainedExp, int gainedEP ){
        GainedEffortPoints += gainedEP;

        if( _level == PokemonSO.MAXLEVEL )
            return;
            
        Exp += gainedExp;
    }

    //--disabled for now just to shut up trylearnmove, will be used for out of battle leveling via candies n shit
    public bool CheckForLevelUp(){
        if( CanLevel() ){
            //--Learn Moves
            if( GetNextLearnableMove() != null ){
                var newMove = GetNextLearnableMove();
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
    }

     public Evolutions CheckForEvolution(){
        return PokeSO.Evolutions.FirstOrDefault( e => e.EvolutionLevel >= _level );
    }

    public void Evolve( Evolutions evo ){
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
            return false;
    }

    public void TryReplaceMove_Battle(  MoveSO newMove, ILearnMoveContext moveMenu, Action<bool> learnMoveComplete, BattleSystem battleSystem ){
        var moveMenuBattle = (LearnMove_Battle)moveMenu;
        moveMenuBattle.Setup( this, ActiveMoves.Select( x => x.MoveSO ).ToList(), newMove, learnMoveComplete );
        battleSystem.PlayerBattleMenu.OnPushNewState?.Invoke( battleSystem.PlayerBattleMenu.MoveLearnSelectionState );
    }

    public void TryReplaceMove_Pause( MoveSO newMove, ILearnMoveContext moveMenu, Action<bool> learnMoveComplete, BagScreen_Pause bagScreen ){
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
        return ActiveMoves.Count( m => m.MoveSO == move ) > 0;
    }

    public bool CheckCanLearnMove( MoveSO move ){
        return PokeSO.TeachableMoves.Contains( move );
    }

    private void CalculateStats(){
        Stats = new Dictionary<Stat, int>();

        Stats.Add( Stat.Attack,    Mathf.FloorToInt((( 2 * PokeSO.Attack    * ( MathF.Max( CalcEVs( ATK_EVs ),   1f ))) * Level ) / 100f ) + 5 );
        Stats.Add( Stat.Defense,   Mathf.FloorToInt((( 2 * PokeSO.Defense   * ( MathF.Max( CalcEVs( DEF_EVs ),   1f ))) * Level ) / 100f ) + 5 );
        Stats.Add( Stat.SpAttack,  Mathf.FloorToInt((( 2 * PokeSO.SpAttack  * ( MathF.Max( CalcEVs( SPATK_EVs ), 1f ))) * Level ) / 100f ) + 5 );
        Stats.Add( Stat.SpDefense, Mathf.FloorToInt((( 2 * PokeSO.SpDefense * ( MathF.Max( CalcEVs( SPDEF_EVs ), 1f ))) * Level ) / 100f ) + 5 );
        Stats.Add( Stat.Speed,     Mathf.FloorToInt((( 2 * PokeSO.Speed     * ( MathF.Max( CalcEVs( SPE_EVs ),   1f ))) * Level ) / 100f ) + 5 );

        MaxHP = Mathf.FloorToInt( ( 2 * PokeSO.MaxHP * Level ) / 100 ) + Level + 10;
    }

    private float CalcEVs( int statEVs ){
        int value = statEVs / 4;

        return Mathf.FloorToInt( value );
    }

    //--make private, this was for quick testing
    private int GetStat( Stat stat ){
        int statValue = Stats[stat];

        int boost = StatBoost[stat];
        var changeModifier = new float[] { 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f };
        var directModifier = DirectStatChange[stat].Aggregate( 1.0f, (acc, val) => acc * val );

        // Debug.Log( $"The Modifier value applied to {stat} is: {directModifier}" );

        //--Apply Direct Stat Change (Burn, Paralysis, Ruin Ability, etc.)
        // Debug.Log( $"Stat Value before direct modifier: {statValue}" );
        statValue = Mathf.FloorToInt( statValue * directModifier );
        // Debug.Log( $"Stat Value after direct modifier: {statValue}" );

        if( boost >= 0 )
            statValue = Mathf.FloorToInt( statValue * changeModifier[boost] );
        else
            statValue = Mathf.FloorToInt( statValue / changeModifier[-boost] );

        return statValue;
    }

    public void ApplyStatChange( List<StatBoost> statBoosts ){
        foreach( var boost in statBoosts ){
            var stat = boost.Stat;
            var change = boost.Change;

            StatBoost[stat] = Mathf.Clamp( StatBoost[stat] + change, -6, 6 );

            if( change == 1 )
                StatusChanges.Enqueue( $"{PokeSO.Name}'s {stat} rose by {change} stage!" );
            if( change > 1 )
                StatusChanges.Enqueue( $"{PokeSO.Name}'s {stat} sharply rose by {change} stages!" );
            if( change == 6 )
                StatusChanges.Enqueue( $"{PokeSO.Name}'s {stat} maxed out at stage {change}!" );
            if( change == -1 )
                StatusChanges.Enqueue( $"{PokeSO.Name}'s {stat} decreased by {change} stage!" );
            if( change < -1 )
                StatusChanges.Enqueue( $"{PokeSO.Name}'s {stat} sharply decreased by {change} stages!" );
            if( change == -6 )
                StatusChanges.Enqueue( $"{PokeSO.Name}'s {stat} bottomed out at stage {change}!" );
        }
    }

    public void ApplyDirectStatChange( Stat stat, float change ){
        Debug.Log( $"Apply Direct Stat Change: {stat} x {change}" );
        
        if( DirectStatChange.ContainsKey( stat ) )
            DirectStatChange[stat].Add( change );
        else
            Debug.LogError( "Stat not found!" );

    }

    public void RemoveDirectStatChange( Stat stat, float change ){
        Debug.Log( $"Apply Direct Stat Change: {stat} x {change}" );
        
        if( DirectStatChange.ContainsKey( stat ) && DirectStatChange[stat].Contains( change ) )
            DirectStatChange[stat].Remove( change );
        else
            Debug.LogError( "Stat or Modifier not found!" );
    }

    private void ResetStatChanges(){
        StatBoost = new Dictionary<Stat, int>()
        {
            { Stat.Attack,    0 },
            { Stat.Defense,   0 },
            { Stat.SpAttack,  0 },
            { Stat.SpDefense, 0 },
            { Stat.Speed,     0 },
            { Stat.Accuracy,  0 },
            { Stat.Evasion,   0 },
        };

        DirectStatChange = new Dictionary<Stat, List<float>>
        {
            { Stat.Attack,      new() { 1f } },
            { Stat.Defense,     new() { 1f } },
            { Stat.SpAttack,    new() { 1f } },
            { Stat.SpDefense,   new() { 1f } },
            { Stat.Speed,       new() { 1f } },

        };
    }

    public void IncreaseHP( int amount ){
        CurrentHP = Mathf.Clamp( CurrentHP + amount, 0, MaxHP );
        OnDisplayInfoChanged?.Invoke();
    }

    public void DecreaseHP( int damage ){
        CurrentHP = Mathf.Clamp( CurrentHP - damage, 0, MaxHP );
        OnDisplayInfoChanged?.Invoke();
    }

    private void UpdateHPOnLevelup( int previousMaxHP ){
        float currentPercentage = previousMaxHP / CurrentHP;
        int newCurrentHP = Mathf.FloorToInt( currentPercentage * MaxHP );
        CurrentHP = newCurrentHP;
    }

    public void SetSevereStatus( ConditionID conditionID ){
        if( SevereStatus != null && conditionID == ConditionID.FNT ){
            SevereStatus = ConditionsDB.Conditions[conditionID];
        }
        else if( SevereStatus != null )
            return;

        SevereStatus = ConditionsDB.Conditions[conditionID];

        if( GameStateController.Instance.CurrentStateEnum == GameStateController.GameStateEnum.BattleState ){
            SevereStatus?.OnApplyStatus?.Invoke( this );
            SevereStatus?.OnStart?.Invoke( this );
            StatusChanges.Enqueue( $"{_pokeSO.Name} {SevereStatus.StartMessage}" );
        }

        Debug.Log( $"{_pokeSO.Name} {SevereStatus.StartMessage}" );
        OnStatusChanged?.Invoke();
    }

    public void CureSevereStatus(){
        if( SevereStatus != null && SevereStatus.ID == ConditionID.BRN )
            RemoveDirectStatChange( Stat.Attack, 0.5f );

        if( SevereStatus != null && SevereStatus.ID == ConditionID.FBT )
            RemoveDirectStatChange( Stat.SpAttack, 0.5f );

        if( SevereStatus != null && SevereStatus.ID == ConditionID.PAR )
            RemoveDirectStatChange( Stat.Speed, 0.25f );

        SevereStatus = null;
        OnStatusChanged?.Invoke(); //--For now this just sets the severe status icon in the battlehud
    }

    public void SetVolatileStatus( ConditionID conditionID ){
        if( VolatileStatus != null )
            return;

        VolatileStatus = ConditionsDB.Conditions[conditionID];
        VolatileStatus?.OnStart?.Invoke( this );
        Debug.Log( $"{_pokeSO.Name} has been afflicted with: {ConditionsDB.Conditions[conditionID].Name}" );
        // OnStatusChanged?.Invoke(); -------will add some visual effect for volatile statuses eventually
    }

    public void CureVolatileStatus(){
        VolatileStatus = null;
        // OnStatusChanged?.Invoke(); -------will add some visual effect for volatile statuses eventually
    }

    public Move GetRandomMove(){
        int r = UnityEngine.Random.Range( 0, ActiveMoves.Count );
        return ActiveMoves[r];
    }

    public void OnApplyStatus(){
        SevereStatus?.OnApplyStatus?.Invoke( this );
    }

    public bool OnBeforeTurn(){
        if( SevereStatus?.OnBeforeTurn != null )
            return SevereStatus.OnBeforeTurn( this );

        //--Volatile Status
        if( VolatileStatus?.OnBeforeTurn != null )
            return VolatileStatus.OnBeforeTurn( this );

        return true;
    }

    public void OnAfterTurn(){
        SevereStatus?.OnAfterTurn?.Invoke( this );
        VolatileStatus?.OnAfterTurn?.Invoke( this );
    }

}

public class DirectStatChange
{
    public Stat Stat;
    public float Change;
}

public class DamageDetails
{
    public bool Fainted { get; set; }
    public float Critical { get; set; }
    public float TypeEffectiveness { get; set; }
}

[Serializable]
public class PokemonSaveData
{
    public PokemonSpecies Species;
    public string Nickname;
    public int Level;
    public PokeBallType CurrentBall;
    public int CurrentHP;
    public int Exp;
    public int GainedEffortPoints;
    public int HP_EVs;
    public int ATK_EVs;
    public int DEF_EVs;
    public int SPATK_EVs;
    public int SPDEF_EVs;
    public int SPE_EVs;
    public ConditionID? SevereStatus;
    public List<MoveSaveData> ActiveMoves;
    public List<MoveSaveData> LearnedMoves;
    public bool IsPlayerUnit;
}
