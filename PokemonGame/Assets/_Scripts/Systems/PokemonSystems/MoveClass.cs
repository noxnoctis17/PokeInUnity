using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveClass
{
    public MoveBaseSO MoveSO { get; set; }
    public int PP { get; set; }

    public MoveClass( MoveBaseSO mBase ){
        MoveSO = mBase;
        PP = MoveSO.PP;
    }
}
