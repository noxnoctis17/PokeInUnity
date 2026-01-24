using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IRoundEndPhaseHandler
{
    public void OnPhaseTick( BattleSystem battleSystem ){}
    public void OnUnitTick( BattleSystem battleSystem, BattleUnit unit ){}
}
