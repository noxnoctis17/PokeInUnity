using System;
using UnityEngine;

public class OnFieldUnit : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    private PokemonClass _pokemon;
    private int _damageTaken;

    private void OnEnable(){
        // BattleUnit.OnDamageTaken += ShowDamageTaken;
    }

    public void Setup( PokemonClass pokemon ){
        _pokemon = pokemon;
        _spriteRenderer.sprite = pokemon.PokeSO.FrontSprite;
    }
}
