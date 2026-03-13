using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent( typeof( BattleAI ) )]
// [Serializable]
public class BattleUnit : MonoBehaviour
{
    [SerializeField] private int _level;
    [SerializeField] private bool _isAI;
    private BattleSystem _battleSystem;
    private BattleAI _battleAI;
    public BattleAI BattleAI => _battleAI;
    public int Level => _level;
    public BattleTrainer Trainer { get; private set; }
    public BattleHUD BattleHUD { get; set; }
    public PokemonSO PokeSO { get; private set; } //--why the fuck is this public //--03/26/24 still don't know why this is public lol //--04/08/24 decided to just make it into a property finally lol //--11/25/25 pretty sure we actually use it now
    public Pokemon Pokemon { get; set; }
    public Pokemon DamagedBy { get; private set; }
    public Pokemon InfatuationTarget { get; private set; }
    public Pokemon ImprisonedBy { get; private set; }
    public ItemSO RemovedHeldItem { get; private set; }
    public bool IsAI => _isAI;
    public PokemonAnimator PokeAnimator { get; private set; }
    public Transform PokeTransform { get; private set; } //--quick ref for battle tweens. tweens need to target the gameobject named "Pokemon" that holds the animator and shadow animator objects for a mon in battle.
    public Dictionary<UnitFlags, BattleUnitFlag> Flags { get; private set; }
    private const float SCREENS_MODIFIER = 0.66796875f;
    private const float AURORA_VEIL_MODIFIER = 0.6669921875f;
    public Move LastUsedMove { get; private set; }
    public Queue<BattleUnitFlag> AfterNextRoundQueue { get; private set; }

    private void OnEnable()
    {
        PokeAnimator = GetComponentInChildren<PokemonAnimator>();
    }

    public void SetAI( bool value )
    {
        _isAI = value;
    }

    public void Setup( Pokemon pokemon, BattleTrainer trainer, BattleHUD battleHUD, BattleSystem battleSystem )
    {
        _battleSystem = battleSystem;
        _battleAI = GetComponent<BattleAI>();
        BattleHUD = battleHUD;
        Trainer = trainer;

        AfterNextRoundQueue = new();

        InitFlags();

        UpdateUnit( pokemon );
        
        PokeAnimator.SetBattleSystem( battleSystem );
        PokeTransform = PokeAnimator.PokemonTransform;

        Flags[UnitFlags.SuccessiveProtectUses].Count = 0;

        if( _isAI )
        {
            _battleAI.enabled = true;
            GetComponent<BattleAI>().InitializeAI( _battleSystem, this );
            UpdateAITeamPieceValue();
        }
        else
        {
            _battleAI.enabled = false;
        }
    }

    public void UpdateUnit( Pokemon pokemon )
    {
        Pokemon = pokemon;
        _level = pokemon.Level;
        PokeSO = pokemon.PokeSO;

        if( !Pokemon.Equals( _battleSystem.WildPokemon ) )
            PokeAnimator.Initialize( PokeSO );

        BattleHUD.SetData( Pokemon, this );

        DecideIfGrounded();

        if( _isAI )
            _battleAI.ResetSetupAmount();
    }

    public void UpdateAITeamPieceValue()
    {
        var allyTeam = _battleSystem.GetAllyParty( Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
        _battleAI.RefreshTeamPieceValues( allyTeam );
    }

    public void TempUsage( Pokemon pokemon )
    {
        Pokemon = pokemon;
    }

    private void InitFlags()
    {
        Flags = new()
        {
            { UnitFlags.TurnsTaken,             new() },
            { UnitFlags.SuccessiveProtectUses,  new() },
            { UnitFlags.DidDamage,              new() },
            { UnitFlags.Reflect,                new() },
            { UnitFlags.LightScreen,            new() },
            { UnitFlags.AuroraVeil,             new() },
            { UnitFlags.ChoiceItem,             new() },
            { UnitFlags.Phazed,                 new() },
            { UnitFlags.Trapped,                new() },
            { UnitFlags.Ungrounded,             new() },
            { UnitFlags.Prankster,              new() },
            { UnitFlags.Imprisoned,             new() },
            { UnitFlags.Substitute,             new() },
            { UnitFlags.CompletedTurn,          new() },
            { UnitFlags.SkillSwapped,           new() },
            { UnitFlags.BatonPass,              new() { StatStages = new(), VolatileStatuses = new() } },
            { UnitFlags.SemiInvulnerable,       new() },
            { UnitFlags.TwoTurnMove,            new() },
            { UnitFlags.Recharging,             new() },
            { UnitFlags.Charging,               new() },
            { UnitFlags.Minimized,              new() },
            { UnitFlags.IncreasedStatStage,     new() },
            { UnitFlags.LoweredStatStage,       new() },
            { UnitFlags.TookDamage,             new() },
            { UnitFlags.Wish,                   new() },
            { UnitFlags.FutureSight,            new() },
            { UnitFlags.DoomDesire,             new() },
            { UnitFlags.FaintedPreviousTurn,    new() },
        };
    }

    public void SetFlagCount( UnitFlags flag, int count )
    {
        Flags[flag].Count = count;
    }

    public void SetFlagActive( UnitFlags flag, bool active )
    {
        Debug.Log( $"Setting flag {flag} to: {active}" );
        Flags[flag].IsActive = active;
    }

    public void IncreaseTurnsTakenInBattle()
    {
        int turnsTaken = Flags[UnitFlags.TurnsTaken].Count;
        turnsTaken++;
        SetFlagCount( UnitFlags.TurnsTaken, turnsTaken );
        Debug.Log( $"{Pokemon.NickName}'s Turn Count: {Flags[UnitFlags.TurnsTaken].Count}" );
    }

    public void ResetTurnsTakenInBattle()
    {
        int turnsTaken = -1;
        Debug.Log( $"{Pokemon.NickName}'s Turn Count: {Flags[UnitFlags.TurnsTaken].Count}" );
        SetFlagCount( UnitFlags.TurnsTaken, turnsTaken );
        Debug.Log( $"{Pokemon.NickName}'s Turn Count: {Flags[UnitFlags.TurnsTaken].Count}" );
    }

    public void SetLastUsedMove( Move move )
    {
        LastUsedMove = move;
    }

    public void SetInfatuationTarget( Pokemon target )
    {
        InfatuationTarget = target;
    }

    public void SetImprisonedBy( Pokemon by )
    {
        ImprisonedBy = by;
    }

    public bool IsChoiceItemLocked()
    {
        if( Flags[UnitFlags.ChoiceItem].IsActive && LastUsedMove != null )
            return true;
        else
            return false;
    }

    public void SetUnitTrapped( bool value )
    {
        if( !Pokemon.CheckTypes( PokemonType.Ghost ) )
        {
            SetFlagActive( UnitFlags.Trapped, value );
        }
    }

    public void SetDidDamage( BattleUnit attacker, BattleUnit target, Move move, int damage )
    {
        Flags[UnitFlags.DidDamage].IsActive = true;
        Flags[UnitFlags.DidDamage].Attacker = attacker;
        Flags[UnitFlags.DidDamage].Target = target;
        Flags[UnitFlags.DidDamage].Move = move;
        Flags[UnitFlags.DidDamage].Count = damage;
    }

    public void ClearDidDamage()
    {
        Flags[UnitFlags.DidDamage].IsActive = false;
        Flags[UnitFlags.DidDamage].Attacker = null;
        Flags[UnitFlags.DidDamage].Move = null;
        Flags[UnitFlags.DidDamage].Count = 0;
    }

    public void SetTookDamage( BattleUnit attacker, BattleUnit target, Move move, int damage )
    {
        Flags[UnitFlags.TookDamage].IsActive = true;
        Flags[UnitFlags.TookDamage].Attacker = attacker;
        Flags[UnitFlags.TookDamage].Target = target;
        Flags[UnitFlags.TookDamage].Move = move;
        Flags[UnitFlags.TookDamage].Count = damage;
    }

    public void ClearTookDamage()
    {
        Flags[UnitFlags.TookDamage].IsActive = false;
        Flags[UnitFlags.TookDamage].Attacker = null;
        Flags[UnitFlags.TookDamage].Move = null;
        Flags[UnitFlags.TookDamage].Count = 0;
    }

    public void DecideIfGrounded()
    {
        if( Pokemon.CheckTypes( PokemonType.Flying ) || Pokemon.AbilityID == AbilityID.Levitate || Pokemon.BattleItemEffect?.ID == BattleItemEffectID.AirBalloon )
            SetFlagActive( UnitFlags.Ungrounded, true );
        else
            SetFlagActive( UnitFlags.Ungrounded, false );
    }

    public void SetSubstitute()
    {
        int hp = Mathf.FloorToInt( Pokemon.MaxHP * 0.25f );
        Flags[UnitFlags.Substitute].IsActive = true;
        Flags[UnitFlags.Substitute].SubstituteHP = hp;
    }

    public void SetCharging( BattleUnit target, Move move )
    {
        SetFlagActive( UnitFlags.Charging, true );
        Flags[UnitFlags.Charging].Move = move;
        Flags[UnitFlags.Charging].Target = target;
    }

    public void ClearCharging()
    {
        SetFlagActive( UnitFlags.Charging, false );
        Flags[UnitFlags.Charging].Count = 0;
        Flags[UnitFlags.Charging].Move = null;
    }

    public void SetWish( BattleUnit user, Move move )
    {
        SetFlagActive( UnitFlags.Wish, true );
        var wish = Flags[UnitFlags.Wish];
        wish.User = user.Pokemon;
        wish.Count = 1;
        wish.Move = move;

        AfterNextRoundQueue.Enqueue( wish );
    }

    public void ClearWish()
    {
        SetFlagActive( UnitFlags.Wish, false );
        var wish = Flags[UnitFlags.Wish];
        wish.Attacker = null;
        wish.Count = 0;
    }

    public void SetFutureSight( BattleUnit user, Move move )
    {
        SetFlagActive( UnitFlags.FutureSight, true );
        var fs = Flags[UnitFlags.FutureSight];
        fs.Attacker = user;
        fs.User = user.Pokemon;
        fs.Count = 2;
        fs.Move = move;

        AfterNextRoundQueue.Enqueue( fs );
    }

    public void ClearFutureSight()
    {
        SetFlagActive( UnitFlags.FutureSight, false );
        var fs = Flags[UnitFlags.FutureSight];
        fs.Attacker = null;
        fs.User = null;
        fs.Count = 0;
        fs.Move = null;
    }

    public void SetBatonPass( BattleUnit user )
    {
        SetFlagActive( UnitFlags.BatonPass, true );
        var pass = Flags[UnitFlags.BatonPass];
        pass.User = user.Pokemon;
        pass.StatStages = user.Pokemon.GetStatStages();
        pass.VolatileStatuses = user.Pokemon.GetBatonPassStatuses();
    }

    public void ClearBatonPass()
    {
        SetFlagActive( UnitFlags.BatonPass, false );
        var pass = Flags[UnitFlags.BatonPass];
        pass.User = null;
        pass.StatStages.Clear();
        pass.VolatileStatuses.Clear();
    }

    public void ClearAfterNextRoundQueue()
    {
        AfterNextRoundQueue.Clear();
    }

    private float GetHelpingHand( BattleUnit attacker )
    {
        if( attacker.Pokemon.VolatileStatuses.ContainsKey( VolatileConditionID.HelpingHand ) )
            return 1.5f;
        else
            return 1f;
    }

    private float GetBurnOrFrostbite( BattleUnit attacker, Move move )
    {
        if( attacker.Pokemon.Ability?.ID == AbilityID.Guts )
            return 1f;

        if( move.MoveSO.Name == "Facade" )
            return 1f;

        if( attacker.Pokemon.SevereStatus != null )
        {
            if( move.MoveSO.MoveCategory == MoveCategory.Physical && attacker.Pokemon.SevereStatus.ID == SevereConditionID.BRN )
                return 0.5f;
            else if( move.MoveSO.MoveCategory == MoveCategory.Special && attacker.Pokemon.SevereStatus.ID == SevereConditionID.FBT )
                return 0.5f;
            else
                return 1f;
        }
        else
            return 1f;
    }

    public DamageDetails TakeDamage( Move move, BattleUnit attacker, WeatherCondition weather, TerrainCondition terrain, int targetCount, int hit )
    {
        var target = Pokemon;
        var category = move.MoveSO.MoveCategory;
        float critical = 1f;

        float targets = 1f;
        if( targetCount > 1 )
            targets = 0.75f;

        Debug.Log( $"[Perform Move Command][Battle Unit][Take Damage] Target Modifier is: {targets}" );

        //--Calculate crit chance in accordance to move's crit behavior
        if( move.MoveSO.CritBehavior != CritBehavior.NeverCrits )
        {
            if( move.MoveSO.CritBehavior == CritBehavior.AlwaysCrits )
            {
                critical = 1.5f;
            }
            else
            {
                //--I barely understand this math LOL 05/29/24
                int critChance = 0 + ( move.MoveSO.CritBehavior == CritBehavior.HighCritRatio ? 1 : 0 ); //--TODO Ability checks (sniper?), held item (scope lens?)
                float[] chances = new float[] { 4.167f, 12.5f, 50f, 100f, };
                if( UnityEngine.Random.value * 100f <= chances[Mathf.Clamp( critChance, 0, 3 )] )
                    critical = 1.5f;
            }
        }

        //--STAB. If either of the attacker's types match the move types, return 1.5f, otherwise, return 1f. Adaptability returns 2f, otherwise it returns whatever STAB is.
        float STAB = attacker.Pokemon.CheckTypes( move.MoveType ) ? 1.5f : 1f;
        STAB = attacker.Pokemon.Ability?.OnSTABModify?.Invoke( attacker.Pokemon, move ) ?? STAB;

        //--Move Type vs target type effectiveness
        float effectiveness = TypeChart.GetEffectiveness( move.MoveType, target.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveType, target.PokeSO.Type2 );

        //--Weather damage modifier
        float weatherModifier = weather?.OnDamageModify?.Invoke( attacker.Pokemon, Pokemon, move ) ?? 1f;
        Debug.Log( $"[Take Damage] Weather Modifier: {weatherModifier}" );

        //--Terrain damage modifier
        float terrainModifier = terrain?.OnDamageModify?.Invoke( attacker, Pokemon, move ) ?? 1f;
        Debug.Log( $"[Take Damage] Terrain Modifier: {terrainModifier}" );

        //--Screens damage modifiers
        float reflectModifier = 1f;
        float lightScreenModifier = 1f;
        float auroraVeilModifier = 1f;

        //--Actually set Screens Damage Reduction modifiers if applicable
        if( attacker.Pokemon.Ability?.ID != AbilityID.Infiltrator )
        {
            if( Flags[UnitFlags.Reflect].IsActive && move.MoveSO.MoveCategory == MoveCategory.Physical )
                reflectModifier = SCREENS_MODIFIER;

            if( Flags[UnitFlags.LightScreen].IsActive && move.MoveSO.MoveCategory == MoveCategory.Special )
                lightScreenModifier = SCREENS_MODIFIER;

            if( Flags[UnitFlags.AuroraVeil].IsActive )
                auroraVeilModifier = AURORA_VEIL_MODIFIER;
        }

        //--Held item damage modifier
        float itemOnDamageModify = attacker.Pokemon.BattleItemEffect?.OnDamageModify?.Invoke( attacker, target, move ) ?? 1f;
        
        var damageDetails = new DamageDetails()
        {
            TypeEffectiveness = effectiveness,
            Critical = critical,
            Fainted = false,
            DamageDealt = 0,
        };

        float attackStat = 0;
        float defenseStat = 0;
        if( move.MoveSO.MoveCategory == MoveCategory.Physical )
        {
            attackStat = attacker.Pokemon.Attack;
            defenseStat = target.Defense;

            //--In case of ability modifying attack stat, such as Blaze, Torrent, or Overgrow.
            attackStat = attacker.Pokemon.Modify_ATK( attackStat, target, move );
            defenseStat = target.Modify_DEF( defenseStat, attacker.Pokemon, move );
        }
        else if( move.MoveSO.MoveCategory == MoveCategory.Special )
        {
            attackStat = attacker.Pokemon.SpAttack;
            defenseStat = target.SpDefense;

            //--In case of ability modifying attack stat, such as Blaze, Torrent, or Overgrow.
            attackStat = attacker.Pokemon.Modify_SpATK( attackStat, target, move );
            defenseStat = target.Modify_SpDEF( defenseStat, attacker.Pokemon, move );
        }

        if( MoveConditionDB.Conditions.ContainsKey( move.MoveSO.Name ) )
        {
            attackStat = MoveConditionDB.Conditions[move.MoveSO.Name].OnOverrideAttackingStat?.Invoke( attacker, this, move ) ?? attackStat;
            defenseStat = MoveConditionDB.Conditions[move.MoveSO.Name].OnOverrideDefensiveStat?.Invoke( attacker, this, move ) ?? defenseStat;
        }

        //--Apply any damage modifications to the attacker based on the target's ability. In the only existing case atm, Thick Fat reduces both the atk and spatk of the attacker if the move is ice or fire. --12/21/25
        attackStat = target.Ability?.OnModifyTakeDamage?.Invoke( attackStat, attacker.Pokemon, target, move ) ?? attackStat;
        int power = move.MovePower;

        if( MoveConditionDB.Conditions.ContainsKey( move.MoveSO.Name ) )
            power = MoveConditionDB.Conditions[move.MoveSO.Name].OnModifyMovePower?.Invoke( attacker, this, move, hit ) ?? power;

        float helpingHand = GetHelpingHand( attacker );
        float brnORfbt = GetBurnOrFrostbite( attacker, move );
        
        float random = UnityEngine.Random.Range( 0.85f, 1f );

        float modifiers = targets * random * STAB * effectiveness * critical * weatherModifier * terrainModifier * reflectModifier * lightScreenModifier * auroraVeilModifier
                            * itemOnDamageModify * helpingHand * brnORfbt;

        float damageCalc = Mathf.Floor( ( 2f * attacker.Level / 5f + 2f ) * power * attackStat / defenseStat / 50f + 2f ) * modifiers;
        int rawDamage = (int)Mathf.Max( damageCalc, 1f );
        int damage = Mathf.Clamp( rawDamage, 1, Pokemon.CurrentHP );

        if( effectiveness == 0 )
            damage = 0;

        if( MoveConditionDB.Conditions.ContainsKey( move.MoveSO.Name ) )
            damage = MoveConditionDB.Conditions[move.MoveSO.name].OnModifyMoveDamage?.Invoke( attacker, this, move, damage ) ?? damage;

        //--Mostly just for focus sash i think.
        damage = Pokemon.BattleItemEffect?.OnTakeMoveDamage?.Invoke( attacker, this, move, damage ) ?? damage;

        //--This is essentially just for endure i think, as of 02/05/26
        damage = Pokemon.TransientStatus?.OnTakeDamage?.Invoke( this, damage ) ?? damage;

        //--Substitute
        if( Flags[UnitFlags.Substitute].IsActive && ( attacker.Pokemon.Ability?.ID != AbilityID.Infiltrator || !move.MoveSO.Flags.Contains( MoveFlags.Authentic ) ))
        {
            int subHP = Flags[UnitFlags.Substitute].SubstituteHP;
            Flags[UnitFlags.Substitute].SubstituteHP = Mathf.Clamp( subHP, 0, subHP - damage );

            _battleSystem.AddDialogue( $"{Pokemon.NickName}'s substitute took the damage for it!" );

            if( Flags[UnitFlags.Substitute].SubstituteHP <= 0 )
            {
                _battleSystem.AddDialogue( $"{Pokemon.NickName}'s substitute broke!" );
                Flags[UnitFlags.Substitute].IsActive = false;
            }
        }
        else
            target.DecreaseHP( damage );

        damageDetails.DamageDealt = damage;
        DamagedBy = attacker.Pokemon;

        SetTookDamage( attacker, this, move, damage );
        return damageDetails;
    }

    public void TakeRecoilDamage( int damage )
    {
        if( damage < 1 )
            damage = 1;

        Pokemon.DecreaseHP( damage );
        Pokemon.AddStatusEvent( StatusEventType.Damage, $"{Pokemon.NickName} was hurt by recoil!" );
    }
}

public enum UnitFlags
{
    TurnsTaken,
    SuccessiveProtectUses,
    DidDamage,
    Reflect,
    LightScreen,
    AuroraVeil,
    ChoiceItem,
    Phazed,
    Trapped,
    Ungrounded,
    Prankster,
    Imprisoned,
    Substitute,
    CompletedTurn,
    SkillSwapped,
    BatonPass,
    SemiInvulnerable,
    TwoTurnMove,
    Recharging,
    Charging,
    Minimized,
    IncreasedStatStage,
    LoweredStatStage,
    TookDamage,
    Wish,
    FutureSight,
    DoomDesire,
    FaintedPreviousTurn,
}

public class BattleUnitFlag
{
    public bool IsActive { get; set; }
    public int Count { get; set; }
    public int SubstituteHP { get; set; }
    public Move Move { get; set; }
    public BattleUnit Attacker { get; set; }
    public BattleUnit Target { get; set; }
    public Pokemon User { get; set; }
    public List<StatStage> StatStages { get; set; }
    public Dictionary<VolatileConditionID, ( VolatileCondition Condition, int Duration )> VolatileStatuses { get; set; }

}
