using UnityEngine;

[System.Serializable]
public class BattleUnit : MonoBehaviour
{
    [SerializeField] private BattleHUD _battleHUD;
    public BattleHUD BattleHUD => _battleHUD;
    [SerializeField] private OnFieldUnit _onFieldUnit;
    public OnFieldUnit OnFieldUnit => _onFieldUnit;
    [SerializeField] public PokemonSO _pokeSO;
    [SerializeField] private int _level;
    public int Level => _level;
    private BattleAI _battleAI;
    [SerializeField] private bool _isAI;

    public PokemonClass Pokemon {get; set;}

    private void Start(){
        if(_isAI)
        _battleAI = GetComponent<BattleAI>();
    }

    public void Setup(PokemonClass pokemon){
        _pokeSO = pokemon.PokeSO;
        _level = pokemon.Level;
        Pokemon = pokemon;
        _battleHUD.SetData(Pokemon);
        // _onFieldUnit.Setup(_pokeSO);
    }

    public DamageDetails TakeDamage(MoveClass move, PokemonClass attacker){
        var target = Pokemon;
        float critical = 1f;
        if(UnityEngine.Random.value * 100f <= 6.25f) critical = 1.5f;

        float type = TypeChart.GetEffectiveness(move.moveBase.MoveType, target.PokeSO.Type1) * TypeChart.GetEffectiveness(move.moveBase.MoveType, target.PokeSO.Type2);
        
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
        
        float random = UnityEngine.Random.Range(0.85f, 1f);

        float modifiers = random * type * critical;
        float damageCalc = Mathf.Floor(( 2 * attacker.Level / 5 + 2) * move.moveBase.Power * attack / defense / 50 + 2 ) * modifiers;
        int damage = (int)damageCalc;
        
        target.UpdateHP(damage);

        return damageDetails;
    }

}
