using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public abstract class BattleState
{
    protected BattleSystem BattleSystem;

    public BattleState( BattleSystem battleSystem ){
        BattleSystem = battleSystem;
    }
    
    public virtual IEnumerator Start(){
        yield break;
    }
    
}
