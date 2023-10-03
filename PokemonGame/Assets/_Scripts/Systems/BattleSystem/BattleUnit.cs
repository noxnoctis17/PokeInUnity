using System;
using System.Collections;
using UnityEngine;

[RequireComponent( typeof( BattleAI ) )]
[System.Serializable]
public class BattleUnit : MonoBehaviour
{
    public Action OnIsAI;
    private BattleSystem _battleSystem;
    public BattleHUD BattleHUD { get; set; }
    private BattleAI _battleAI;
    public BattleAI BattleAI => _battleAI;
    [SerializeField] public PokemonSO _pokeSO; //--why the fuck is this public
    [SerializeField] private int _level;
    public int Level => _level;
    [SerializeField] private bool _isAI;

    public PokemonClass Pokemon { get; set; }
    private SpriteRenderer _spriteRenderer;

    private void OnEnable(){
        OnIsAI += EnableAI;
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnDisable(){
        OnIsAI -= EnableAI;
    }

    public void Setup( PokemonClass pokemon, BattleHUD battleHUD, BattleSystem battleSystem){
        _battleSystem = battleSystem;
        _battleAI = GetComponent<BattleAI>();
        _pokeSO = pokemon.PokeSO;
        _level = pokemon.Level;
        Pokemon = pokemon;
        _spriteRenderer.sprite = _pokeSO.FrontSprite;
        BattleHUD = battleHUD;
        BattleHUD.SetData( Pokemon );

        if( _isAI ){
            _battleAI.enabled = true;
            Debug.Log( _battleAI + " " + name );
            SetupAI();
        }
        else{
            _battleAI.enabled = false;
        }
    }

    private void EnableAI(){
        _isAI = true;
    }

    private void SetupAI(){
        Debug.Log( "setup ai" );
        GetComponent<BattleAI>().SetupAI( _battleSystem, this );
    }

    public DamageDetails TakeDamage( MoveClass move, PokemonClass attacker ){
        var target = Pokemon;
        float critical = 1f;
        if( UnityEngine.Random.value * 100f <= 6.25f ) critical = 1.5f;

        float type =     TypeChart.GetEffectiveness( move.moveBase.MoveType, target.PokeSO.Type1 )
                       * TypeChart.GetEffectiveness( move.moveBase.MoveType, target.PokeSO.Type2 );
        
        var damageDetails = new DamageDetails(){
            TypeEffectiveness = type,
            Critical = critical,
            Fainted = false
        };

        float attack = 0;
        float defense = 0;
        if( move.moveBase.MoveCategory == MoveCategory.Physical ){
                attack = attacker.Attack;
                defense = target.Defense;
        }
        else if( move.moveBase.MoveCategory == MoveCategory.Special ){
                attack = attacker.SpAttack;
                defense = target.SpDefense;
        }
        
        float random = UnityEngine.Random.Range( 0.85f, 1f );

        float modifiers = random * type * critical;
        float damageCalc = Mathf.Floor( ( 2 * attacker.Level / 5 + 2 ) * move.moveBase.Power * attack / defense / 50 + 2 ) * modifiers;
        int damage = ( int )damageCalc;

        target.UpdateHP( damage );
        ShowDamageTaken( damage, transform.position );

        return damageDetails;
    }
    
    private void ShowDamageTaken( int damage, Vector3 position ){
        DamageTakenPopup.Create( BattleSystem.DamageTakenPopupPrefab.transform, damage, position );
    }

}
