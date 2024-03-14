using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class FollowerPokemon : MonoBehaviour
{
    private PokemonParty _pokemonParty;
    private SpriteRenderer _spriteRenderer;
    private CharacterController _controller;
    [SerializeField] private Transform _playerTransform;
    private Vector3 _playerLastPosition;
    private Vector3 _playerCurrentPosition;
    [SerializeField] private float _speed;

    private void OnEnable(){
        _controller = GetComponent<CharacterController>();
        _pokemonParty = _playerTransform.gameObject.GetComponent<PokemonParty>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _playerCurrentPosition = _playerTransform.position;
    }

    private void Start(){
        SetFollowerPokemon( _pokemonParty.PartyPokemon[0] );
    }

    private void Update(){
        if( _playerCurrentPosition != _playerTransform.position ){
            _playerLastPosition = _playerCurrentPosition;
            _controller.Move( _playerLastPosition * Time.deltaTime * _speed );
            _playerCurrentPosition = _playerTransform.position;
        }
    }

    private void SetFollowerPokemon( PokemonClass pokemon ){
        _spriteRenderer.sprite = pokemon.PokeSO.IdleDownSprites[0];
        //--eventually animate n shit, ya know
    }
}
