using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class SimpleAnimator : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private bool _isUI;
    private SpriteAnimator _spriteAnimator;
    private List<Sprite> _currentAnimSheet;

    private void OnEnable()
    {
        _spriteAnimator = new( _spriteRenderer );
    }

    private void Update()
    {
        if( _isUI )
            _image.sprite = _spriteRenderer.sprite;
        
        if( _currentAnimSheet != null && _currentAnimSheet.Count > 0 )
            _spriteAnimator.HandleUpdate();
    }

    public void SetSpriteSheet( List<Sprite> sheet )
    {
        _currentAnimSheet = sheet;

        _spriteAnimator.AnimationFrames = _currentAnimSheet;
        _spriteAnimator.Start();
    }
}
