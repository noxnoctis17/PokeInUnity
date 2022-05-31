using UnityEngine;
using System.Collections.Generic;
using Pathfinding;
using TMPro;

public class OnFieldUnit : MonoBehaviour
{
    [SerializeField] private float _searchRadius;
    [SerializeField] public TextMeshProUGUI DamageText;
    private AIPath _aiPath;
    [SerializeField] private SpriteRenderer _spriteRenderer;

    private void OnEnable(){
        _aiPath = GetComponent<AIPath>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Setup(PokemonSO pokeSO){
        _spriteRenderer.sprite = pokeSO.FrontSprite;
    }

    private Vector3 FindPosition(){
        var point = Random.onUnitSphere * _searchRadius;
        point.y = 0;
        point += _aiPath.position;
        return point;
    }
}