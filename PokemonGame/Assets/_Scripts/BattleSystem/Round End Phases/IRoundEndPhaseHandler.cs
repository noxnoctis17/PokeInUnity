using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IRoundEndPhaseHandler
{
    public void Apply( BattleSystem battleSystem, BattleUnit unit );
}
