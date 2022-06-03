using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BattleStateMachine
{
    protected BattleState BattleState;

    public void SetState(BattleState state){
        BattleState = state;
    }

}
