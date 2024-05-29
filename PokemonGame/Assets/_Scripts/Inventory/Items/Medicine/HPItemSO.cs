using UnityEngine;

[CreateAssetMenu( menuName = "Items/Medicine/HP Recovery Item" )]
public class HPItemSO : ItemSO
{
    [Header( "HP" )]
    [SerializeField] private int _hpHealAmnt;
    [SerializeField] private bool _restoreMaxHP;
    private int _previousHP;

    public override bool Use( Pokemon pokemon ){
        if( pokemon.SevereStatus != null && pokemon.SevereStatus.ID == ConditionID.FNT )
            return false;

        //--Potion Item
        if( _restoreMaxHP || _hpHealAmnt > 0 ){
            if( pokemon.CurrentHP == pokemon.MaxHP ){
                //--HP is already full!
                return false;
            }

            //--Max Potion
            if( _restoreMaxHP )
                pokemon.IncreaseHP( pokemon.MaxHP );
            else{
                //--Restore by Potion Amount
                _previousHP = pokemon.CurrentHP;
                pokemon.IncreaseHP( _hpHealAmnt );
            }
        }

        return true;
    }

    public override bool CheckIfUsable( Pokemon pokemon ){
        if( pokemon.SevereStatus != null && pokemon.SevereStatus.ID == ConditionID.FNT )
            return false;

        //--Potion Item
        if( _hpHealAmnt > 0 ){
            if( pokemon.CurrentHP == pokemon.MaxHP ){
                //--HP is already full!
                return false;
            }
        }
        
        return true;
    }

    public override string UseText( Pokemon pokemon ){
        int healedBy = pokemon.CurrentHP - _previousHP;
        return $"You used a {ItemName}! {pokemon.PokeSO.Name} recovered {healedBy} HP!";
    }

}
