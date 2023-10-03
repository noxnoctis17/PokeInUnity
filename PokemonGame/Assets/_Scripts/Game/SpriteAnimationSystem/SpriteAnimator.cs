using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpriteAnimator
{   
    //==[ PRIVATE REFERENCES ]==
    private SpriteRenderer _spriteRenderer;
    private float _frameRate;
    private int _currentFrame;
    private float _timer;

    //==[ PROPERTIES ]==
    public List<Sprite> AnimationFrames { get; set; } //--Make private if shit goes fuuunkyyyy
    public List<Sprite> ShadowAnimationFrames { get; set; } //--Make private if shit goes fuuunkyyyy

    public SpriteAnimator( SpriteRenderer spriteRenderer, float frameRate = 0.16f ){
        _spriteRenderer = spriteRenderer;
        _frameRate = frameRate;
    }

    public void Start(){
        _currentFrame = 0;
        _spriteRenderer.sprite = AnimationFrames[0];
        _timer = 0;
    }

    public void HandleUpdate(){
        _timer += Time.deltaTime;

        if( _timer > _frameRate ){
            _currentFrame = ( _currentFrame + 1 ) % AnimationFrames.Count;
            _spriteRenderer.sprite = AnimationFrames[_currentFrame];
            _timer -= _frameRate;
        }
    }
}
