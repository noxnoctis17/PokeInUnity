using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BagPocketIconAtlas : MonoBehaviour
{
    [SerializeField] private Sprite _medicine;
    [SerializeField] private Sprite _balls;
    [SerializeField] private Sprite _tms;
    [SerializeField] private Sprite _battle;
    [SerializeField] private Sprite _training;
    [SerializeField] private Sprite _berry;
    [SerializeField] private Sprite _key;
    public static Dictionary<ItemCategory, Sprite> PocketIcons;

    private void OnEnable(){
        InitializeDictionary();
    }

    private void InitializeDictionary(){
        // Debug.Log( "Initialize Pocket Icon Dictionary" );
        PocketIcons = new()
        {
            { ItemCategory.Medicine, _medicine },
            { ItemCategory.PokeBall, _balls },
            { ItemCategory.TM, _tms },
            { ItemCategory.Battle, _battle },
            { ItemCategory.Training, _training },
            { ItemCategory.Berry, _berry },
            { ItemCategory.KeyItem, _key },

        };
    }
}
