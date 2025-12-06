using System;
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
    public BattleHUD BattleHUD { get; set; }
    public PokemonSO PokeSO { get; private set; } //--why the fuck is this public //--03/26/24 still don't know why this is public lol //--04/08/24 decided to just make it into a property finally lol //--11/25/25 pretty sure we actually use it now
    public Pokemon Pokemon { get; set; }
    public bool IsAI => _isAI;
    public PokemonAnimator PokeAnimator { get; private set; }
    public Transform PokeTransform { get; private set; } //--quick ref for battle tweens. tweens need to target the gameobject named "Pokemon" that holds the animator and shadow animator objects for a mon in battle.
    public Action OnIsAI;
    public int TurnsTakenInBattle { get; private set; }
    public int SuccessiveProtectUses { get; private set; }
    private const float SCREENS_MODIFIER = 0.66796875f;
    private const float AURORA_VEIL_MODIFIER = 0.6669921875f;
    public bool ReflectActive { get; private set; }
    public bool LightScreenActive { get; private set; }
    public bool AuroraVeilActive { get; private set; }

    private void OnEnable(){
        OnIsAI += EnableAI;
        PokeAnimator = GetComponentInChildren<PokemonAnimator>();
    }

    private void OnDisable(){
        OnIsAI -= EnableAI;
    }

    public void Setup( Pokemon pokemon, BattleHUD battleHUD, BattleSystem battleSystem ){
        _battleSystem = battleSystem;
        _battleAI = GetComponent<BattleAI>();

        Pokemon = pokemon;
        _level = pokemon.Level;
        PokeSO = pokemon.PokeSO;

        if( !Pokemon.Equals( _battleSystem.WildPokemon ) ){
            PokeAnimator.Initialize( PokeSO );
        }
        
        PokeAnimator.SetBattleSystem( battleSystem );
        PokeTransform = PokeAnimator.PokemonTransform;

        BattleHUD = battleHUD;
        BattleHUD.SetData( Pokemon, this );

        ResetSuccessiveProtectUses();

        if( _isAI )
        {
            _battleAI.enabled = true;
            // Debug.Log( _battleAI + " " + name );
            SetupAI();
        }
        else
        {
            _battleAI.enabled = false;
        }
    }

    private void EnableAI(){
        _isAI = true;
    }

    private void SetupAI(){
        GetComponent<BattleAI>().SetupAI( _battleSystem, this );
    }

    public void IncreaseTurnsTakenInBattle()
    {
        Debug.Log( $"{Pokemon.NickName}'s taken {TurnsTakenInBattle} turns in battle!" );
        TurnsTakenInBattle++;
        Debug.Log( $"{Pokemon.NickName}'s taken {TurnsTakenInBattle} turns in battle!" );
    }

    public void ResetTurnsTakenInBattle()
    {
        TurnsTakenInBattle = -1;
    }

    public void AddSuccessiveProtectUses()
    {
        SuccessiveProtectUses++;;
    }

    public void ResetSuccessiveProtectUses()
    {
        SuccessiveProtectUses = 0;
    }

    public void SetReflect( bool value )
    {
        ReflectActive = value;
        Debug.Log( $"ReflectActive: {ReflectActive}" );
    }

    public void SetLightScreen( bool value )
    {
        LightScreenActive = value;
        Debug.Log( $"LightScreenActive: {LightScreenActive}" );
    }

    public void SetAuroraVeil( bool value )
    {
        AuroraVeilActive = value;
        Debug.Log( $"AuroraVeilActive: {AuroraVeilActive}" );
    }

    public DamageDetails TakeDamage( Move move, Pokemon attacker, WeatherCondition weather ){
        var target = Pokemon;
        var category = move.MoveSO.MoveCategory;
        float critical = 1f;

        //--Calculate crit chance in accordance to move's crit behavior
        if( move.MoveSO.CritBehavior != CritBehavior.NeverCrits ){
            if( move.MoveSO.CritBehavior == CritBehavior.AlwaysCrits ){
                critical = 1.5f;
            }
            else{
                //--I barely understand this math LOL 05/29/24
                int critChance = 0 + ( move.MoveSO.CritBehavior == CritBehavior.HighCritRatio ? 1 : 0 ); //--TODO Ability checks (sniper?), held item (scope lens?)
                float[] chances = new float[] { 4.167f, 12.5f, 50f, 100f, };
                if( UnityEngine.Random.value * 100f <= chances[Mathf.Clamp( critChance, 0, 3 )] )
                    critical = 1.5f;
            }
        }

        float type =     TypeChart.GetEffectiveness( move.MoveSO.Type, target.PokeSO.Type1 )
                       * TypeChart.GetEffectiveness( move.MoveSO.Type, target.PokeSO.Type2 );

        float weatherModifier = weather?.OnDamageModify?.Invoke( Pokemon, attacker, move ) ?? 1f;
        float reflectModifier = 1f;
        float lightScreenModifier = 1f;
        float auroraVeilModifier = 1f;

        Debug.Log( $"Move Category is: {category}" );

        if( ReflectActive && move.MoveSO.MoveCategory == MoveCategory.Physical )
        {
            reflectModifier = SCREENS_MODIFIER;
            Debug.Log( "Reflect Modifier = Screens_Modifier" );
        }

        if( LightScreenActive && move.MoveSO.MoveCategory == MoveCategory.Special )
            lightScreenModifier = SCREENS_MODIFIER;

        if( AuroraVeilActive )
            auroraVeilModifier = AURORA_VEIL_MODIFIER;

        Debug.Log( $"Screen modifiers are: R: {reflectModifier}, LS: {lightScreenModifier}, AV: {auroraVeilModifier}" );
        
        var damageDetails = new DamageDetails()
        {
            TypeEffectiveness = type,
            Critical = critical,
            Fainted = false,
            DamageDealt = 0,
        };

        float attackStat = 0;
        float defenseStat = 0;
        if( move.MoveSO.MoveCategory == MoveCategory.Physical ){
                attackStat = attacker.Attack;
                defenseStat = target.Defense;

                //--In case of ability modifying attack stat, such as Blaze, Torrent, or Overgrow. Will need to encapsulate this stuff eventually...
                attackStat = attacker.Modify_ATK( attackStat, target, move );
                defenseStat = target.Modify_DEF( defenseStat, attacker, move );
        }
        else if( move.MoveSO.MoveCategory == MoveCategory.Special ){
                attackStat = attacker.SpAttack;
                defenseStat = target.SpDefense;

                //--In case of ability modifying attack stat, such as Blaze, Torrent, or Overgrow. Will need to encapsulate this stuff eventually...
                attackStat = attacker.Modify_SpATK( attackStat, target, move );
                defenseStat = target.Modify_SpDEF( defenseStat, attacker, move );
        }
        
        float random = UnityEngine.Random.Range( 0.85f, 1f );

        float modifiers = random * type * critical * weatherModifier * reflectModifier * lightScreenModifier * auroraVeilModifier;
        float damageCalc = Mathf.Floor( ( 2 * attacker.Level / 5 + 2 ) * move.MoveSO.Power * attackStat / defenseStat / 50 + 2 ) * modifiers;
        int rawDamage = (int)Mathf.Max( damageCalc, 1f );
        int damage = Mathf.Clamp( rawDamage, 1, Pokemon.CurrentHP );

        target.DecreaseHP( damage );
        damageDetails.DamageDealt = damage;

        return damageDetails;
    }

    public void TakeRecoilDamage( int damage ){
        if( damage < 1 )
            damage = 1;

        Pokemon.DecreaseHP( damage );
        Pokemon.AddStatusEvent( StatusEventType.Damage, $"{Pokemon.NickName} is damaged by recoil!" );
    }
}
