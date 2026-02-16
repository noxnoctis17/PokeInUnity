using UnityEngine;

[CreateAssetMenu( menuName = "Items/Medicine/Combo Recovery Item" )]
public class ComboRecoveryItemSO : ItemSO
{
    [Header( "HP" )]
    [SerializeField] private int _hpHealAmnt;
    [SerializeField] private bool _restoreMaxHP;

    [Header( "Status" )]
    [SerializeField] private SevereConditionID _severeStatus;
    [SerializeField] private VolatileConditionID _volatileStatus;
    [SerializeField] private bool _restoreAllStatus; //--Excluding FNT
    [SerializeField] private bool _revive; //--Cure FNT status + heal max hp
    [SerializeField] private bool _maxRevive; //--Cure FNT status + heal max hp

    public override bool Use( Pokemon pokemon )
    {
        if( pokemon.SevereStatus != null && pokemon.SevereStatus.ID == SevereConditionID.FNT )
            return false;
        
        //--Potion Item
        if( _restoreMaxHP && pokemon.CurrentHP != pokemon.MaxHP )
            pokemon.IncreaseHP( pokemon.MaxHP );
        else
            return false;

        //--Status Item
        if( pokemon.SevereStatus == null && pokemon.VolatileStatuses == null )
            return false;

        if( _restoreAllStatus )
        {
            pokemon.CureSevereStatus();
            pokemon.ClearAllVolatileStatus();
        }

        return true;
    }

    public override bool CheckIfUsable( Pokemon pokemon )
    {
        //--Revive Item
        if( _revive || _maxRevive )
            if( pokemon.SevereStatus.ID != SevereConditionID.FNT )
                return false;
        
        if( pokemon.SevereStatus != null && pokemon.SevereStatus.ID == SevereConditionID.FNT )
            return false;

        //--Potion Item
        if( _hpHealAmnt > 0 )
        {
            if( pokemon.CurrentHP == pokemon.MaxHP )
            {
                //--HP is already full!
                return false;
            }
        }

        //--Status Item
        if( _restoreAllStatus || _severeStatus != SevereConditionID.None || _volatileStatus != VolatileConditionID.None )
            if( pokemon.SevereStatus == null && pokemon.VolatileStatuses == null )
                return false;
        
        return true;
    }

    public override string UseText( Pokemon pokemon )
    {
        return $"You used a {ItemName}! {pokemon.NickName} was fully restored!";
    }

}
