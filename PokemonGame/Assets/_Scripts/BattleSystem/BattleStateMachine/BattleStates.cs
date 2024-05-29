using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public abstract class InBattleStates
{
    protected BattleSystem BattleSystem;

    public InBattleStates( BattleSystem battleSystem ){
        BattleSystem = battleSystem;
    }
    
    public virtual IEnumerator Start(){
        yield break;
    }
    
}
