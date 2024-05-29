using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BattleStateMachine : MonoBehaviour
{
    protected InBattleStates BattleState;

    public void SetState( InBattleStates state ){
        BattleState = state;
        StartCoroutine( BattleState.Start() );
    }

}
