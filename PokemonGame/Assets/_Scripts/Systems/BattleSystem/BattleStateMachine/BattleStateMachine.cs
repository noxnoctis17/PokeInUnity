using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BattleStateMachine : MonoBehaviour
{
    protected BattleState BattleState;

    public void SetState( BattleState state ){
        BattleState = state;
        StartCoroutine( BattleState.Start() );
    }

}
