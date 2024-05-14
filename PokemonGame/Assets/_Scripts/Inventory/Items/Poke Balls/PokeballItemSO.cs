using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu( menuName = "Items/Pokeball Item" )]
public class PokeballItemSO : ItemSO
{
    public override bool Use(Pokemon pokemon){
        return true;
    }
}
