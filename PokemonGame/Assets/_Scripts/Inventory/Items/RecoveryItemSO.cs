using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu( menuName = "Items/Recovery Item" )]
public class RecoveryItemSO : ItemSO
{
    [Header( "HP" )]
    [SerializeField] private int _hpHealAmnt;
    [SerializeField] private bool _restoreMaxHP;

    [Header( "PP" )]
    [SerializeField] private int _ppHealAmnt;
    [SerializeField] private bool _restoreMaxPP;

    [Header( "Status" )]
    [SerializeField] private ConditionID _status;
    [SerializeField] private bool _recoverAll; //--Excluding FNT
    [SerializeField] private bool _maxRevive; //--Cure FNT status + heal max hp

    public override bool Use( Pokemon pokemon ){
        if( _hpHealAmnt > 0 ){
            if( pokemon.CurrentHP == pokemon.MaxHP ){
                //--HP is already full!
                return false;
            }
            
            pokemon.IncreaseHP( _hpHealAmnt );
        }

        return true;
    }

}
