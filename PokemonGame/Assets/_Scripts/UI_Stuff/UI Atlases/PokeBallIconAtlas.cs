using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PokeBallIconAtlas : MonoBehaviour
{
    [SerializeField] private Sprite _pokeBall;
    [SerializeField] private Sprite _greatBall;
    [SerializeField] private Sprite _ultraBall;
    public static Dictionary<PokeBallType, Sprite> PokeBallIcons;

    private void OnEnable(){
        InitializeDictionary();
    }

    private void InitializeDictionary(){
        PokeBallIcons = new()
        {
            { PokeBallType.PokeBall,    _pokeBall },
            { PokeBallType.GreatBall,   _greatBall },
            { PokeBallType.UltraBall,   _ultraBall },

        };
    }
}
