using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

[System.Serializable]
public class PokemonClass
{
    [SerializeField] private PokemonSO _pokeSO;
    [SerializeField] private int _level;
    [SerializeField] private Transform _showDamageTakenText; //--these are mine to show their text as pop ups in battle
    [SerializeField] private Transform _showMoveUsedText; //--these are mine to show their text as pop ups in battle
    public PokemonSO PokeSO => _pokeSO;
    public int Level => _level;
    public int Exp { get; private set; }
    public bool IsPlayerUnit { get; private set; }
    public bool IsEnemyUnit { get; private set; }
    public int CurrentHP { get; set; }
    public List<MoveClass> ActiveMoves { get; set; }
    public Dictionary<Stat, int> Stats { get; private set; }
    public Dictionary<Stat, int> StatChange { get; private set; }
    public Dictionary<PokemonType, TypeCardColors> TypeColors { get; private set; }
    public ConditionClass SevereStatus { get; private set; }
    public ConditionClass VolatileStatus { get; private set; }
    public Action OnStatusChanged;
    public int SevereStatusTime { get; set; }
    public int VolatileStatusTime { get; set; }
    public bool HPChanged;

//--------------------------------------------------------------------------------------------
//-----------------------------------[POKEMON STATS]------------------------------------------
//--------------------------------------------------------------------------------------------

    public int MaxHP { get; private set; }
    public int Attack => GetStat( Stat.Attack );
    public int Defense => GetStat( Stat.Defense );
    public int SpAttack => GetStat( Stat.SpAttack );
    public int SpDefense => GetStat( Stat.SpDefense );
    public int Speed => GetStat( Stat.Speed );

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

    public PokemonClass( PokemonSO pokeSO, int level ){
        _pokeSO = pokeSO;
        _level = level;

        Init();
    }

    public PokemonClass( PokemonSaveData saveData ){
        _pokeSO = PokemonDB.GetPokemonBySpecies( saveData.Species );
        _level = saveData.Level;
        CurrentHP = saveData.CurrentHP;
        Exp = saveData.Exp;
        GainedEffortPoints = saveData.GainedEffortPoints;
        HP_EVs = saveData.HP_EVs;
        ATK_EVs = saveData.ATK_EVs;
        DEF_EVs = saveData.DEF_EVs;
        SPATK_EVs = saveData.SPATK_EVs;
        SPDEF_EVs = saveData.SPDEF_EVs;
        SPE_EVs = saveData.SPE_EVs;

        if( saveData.SevereStatus != null )
            SevereStatus = ConditionsDB.Conditions[saveData.SevereStatus.Value];
        else
            SevereStatus = null;

        //--Restore Moves
        ActiveMoves = saveData.ActiveMoves.Select( s => new MoveClass( s ) ).ToList(); //--Active Moves
        // LearnedMoves = saveData.LearnedMoves.Select( s => new MoveClass( s ) ).ToList(); //--Full list of known moves, not including active moves. TODO

        CalculateStats();

        ResetStatChanges();
        VolatileStatus = null;
    }

//--------------------------------------------------------------------------------------------
//-------------------------------------[ FUNCTIONS ]------------------------------------------
//--------------------------------------------------------------------------------------------

    private void OnEnable(){
        BattleSystem.OnBattleEnded += ResetStatChanges;
        BattleSystem.OnBattleEnded += CureVolatileStatus;
    }
    
    public void Init(){
        //--------GENERATE MOVES-----------
        ActiveMoves = new List<MoveClass>();
        foreach( var move in PokeSO.LearnableMoves ){
            if( move.LevelLearned <= Level ){
                ActiveMoves.Add( new MoveClass( move.MoveSO ) );
            }

            if( ActiveMoves.Count >= PokemonSO.MAXMOVES )
                break;
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
    }

    public PokemonSaveData CreateSaveData(){
        var saveData = new PokemonSaveData(){
            Species = _pokeSO.Species,
            Level = _level,
            CurrentHP = CurrentHP,
            Exp = Exp,
            GainedEffortPoints = GainedEffortPoints,
            HP_EVs = HP_EVs,
            ATK_EVs = ATK_EVs,
            DEF_EVs = DEF_EVs,
            SPATK_EVs = SPATK_EVs,
            SPDEF_EVs = SPDEF_EVs,
            SPE_EVs = SPE_EVs,
            SevereStatus = SevereStatus?.ID,
            ActiveMoves = ActiveMoves.Select( m => m.CreateSaveData() ).ToList(),
        };

        return saveData;
    }

    public void SetAsPlayerUnit(){
        IsPlayerUnit = true;
    }

    public void SetAsEnemyUnit(){
        IsEnemyUnit = true;
    }

    public void GainExp( int gainedExp, int gainedEP ){
        GainedEffortPoints += gainedEP;

        if( _level == PokemonSO.MAXLEVEL )
            return;
            
        Exp += gainedExp;
    }

    //--disabled for now just to shut up trylearnmove, will be used for out of battle leveling via candies n shit
    // public bool CheckForLevelUp(){
    //     if( CanLevel() ){
    //         //--Learn Moves
    //         if( GetNextLearnableMove() != null )
    //             TryLearnMove( GetNextLearnableMove() );
            
    //         return true;
    //     }
        
    //     return false;
    // }

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
            
            return true;
        }
        else
            return false;
    }

    public LearnableMoves GetNextLearnableMove(){
        return _pokeSO.LearnableMoves.Where( x => x.LevelLearned == _level ).FirstOrDefault();
    }

    public void TryLearnMove( LearnableMoves newMove, LearnMoveMenu moveMenu, BattleSystem battleSystem = null ){
        if( ActiveMoves.Count < PokemonSO.MAXMOVES ){
            ActiveMoves.Add( new MoveClass( newMove.MoveSO ) );

            if( battleSystem == null )
                DialogueManager.Instance.OnStringDialogueEvent?.Invoke( $"{_pokeSO.pName} learned {newMove.MoveSO.MoveName}!" );
        }
        else{
            //--Forget a Move After Battle
            battleSystem.LearnMoveMenu.gameObject.SetActive( true ); //--Eventually this will simply open the summary screen under the context of learning a new move
            moveMenu.Setup( this, ActiveMoves.Select( x => x.MoveSO ).ToList(), newMove.MoveSO );
        }

    }

    public void ReplaceWithNewMove( MoveBaseSO replacedMove, MoveBaseSO newMove ){
        for( int i = 0; i < ActiveMoves.Count; i++ ){
            if( ActiveMoves[i].MoveSO == replacedMove ){
                ActiveMoves[i] = new MoveClass ( newMove );
            }
        }
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

    private int GetStat( Stat stat ){
        int statValue = Stats[stat];

        int change = StatChange[stat];
        var changeModifier = new float[] { 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f };

        if( change >= 0 )
            statValue = Mathf.FloorToInt( statValue * changeModifier[change] );
        else
            statValue = Mathf.FloorToInt( statValue / changeModifier[-change] );

        return statValue;
    }

    public void ApplyStatChange( List<StatChange> statChanges ){
        foreach( var statChange in statChanges ){
            var stat = statChange.Stat;
            var change = statChange.Change;

            StatChange[stat] = Mathf.Clamp( StatChange[stat] + change, -6, 6 );

            Debug.Log( $"{stat} has been changed to: {StatChange[stat]}" );
        }
    }

    private void ResetStatChanges(){
        StatChange = new Dictionary<Stat, int>(){
            {Stat.Attack,    0},
            {Stat.Defense,   0},
            {Stat.SpAttack,  0},
            {Stat.SpDefense, 0},
            {Stat.Speed,     0},
            {Stat.Accuracy,  0},
            {Stat.Evasion,   0},
        };
    }

    public void UpdateHP( int damage ){
        CurrentHP = Mathf.Clamp( CurrentHP - damage, 0, MaxHP );
        HPChanged = true;
    }

    private void UpdateHPOnLevelup( int previousMaxHP ){
        float currentPercentage = CurrentHP / previousMaxHP;
        // Debug.Log( "current hp percentage: " + currentPercentage );
        int newCurrentHP = Mathf.FloorToInt( currentPercentage * MaxHP );
        // Debug.Log( "new currentHP: " + newCurrentHP );
        CurrentHP = newCurrentHP;
    }

    public void SetSevereStatus( ConditionID conditionID ){
        if( SevereStatus != null ) return;

        SevereStatus = ConditionsDB.Conditions[conditionID];
        SevereStatus?.OnRoundStart?.Invoke( this );
        Debug.Log($"{_pokeSO.pName} has been afflicted with: {ConditionsDB.Conditions[conditionID].ConditionName}");
        OnStatusChanged?.Invoke(); //--For now this just sets the severe status icon in the battlehud
    }

    public void CureSevereStatus(){
        SevereStatus = null;
        OnStatusChanged?.Invoke(); //--For now this just sets the severe status icon in the battlehud
    }

    public void SetVolatileStatus( ConditionID conditionID ){
        if( VolatileStatus != null ) return;

        VolatileStatus = ConditionsDB.Conditions[conditionID];
        VolatileStatus?.OnRoundStart?.Invoke( this );
        Debug.Log( $"{_pokeSO.pName} has been afflicted with: {ConditionsDB.Conditions[conditionID].ConditionName}" );
        // OnStatusChanged?.Invoke(); -------will add some visual effect for volatile statuses eventually
    }

    public void CureVolatileStatus(){
        VolatileStatus = null;
        // OnStatusChanged?.Invoke(); -------will add some visual effect for volatile statuses eventually
    }

    public MoveClass GetRandomMove(){
        int r = UnityEngine.Random.Range( 0, ActiveMoves.Count );
        return ActiveMoves[r];
    }

    public bool OnBeforeTurn(){
        bool canPerformMove = true;
        if(SevereStatus?.OnBeforeTurn != null){
            if(!SevereStatus.OnBeforeTurn(this))
                canPerformMove = false;
        }

        if(VolatileStatus?.OnBeforeTurn != null){
            if(!VolatileStatus.OnBeforeTurn(this))
                canPerformMove = false;
        }

        return canPerformMove;
    }

    public void OnAfterTurn(){
        SevereStatus?.OnAfterTurn?.Invoke( this );
        VolatileStatus?.OnAfterTurn?.Invoke( this );
    }

}

public class DamageDetails
{
    public bool Fainted {get; set;}
    public float Critical {get; set;}
    public float TypeEffectiveness {get; set;}
}

[Serializable]
public class PokemonSaveData
{
    public PokemonSpecies Species;
    public string Nickname;
    public int Level;
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
}
