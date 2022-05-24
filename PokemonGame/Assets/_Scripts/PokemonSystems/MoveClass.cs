using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveClass
{
    public MoveBaseSO moveBase {get; set;}
    public int PP {get; set;}

    public MoveClass(MoveBaseSO mBase)
    {
        moveBase = mBase;
        PP = moveBase.PP;
    }
}
