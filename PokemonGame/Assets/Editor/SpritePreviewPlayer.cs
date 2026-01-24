using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

class SpritePreviewPlayer
{
    private List<Sprite> _currentSheet = new();
    private int _frameIndex;
    private double _lastTime;
    private float _fps = 6.25f;
    private bool _playing;
    public List<Sprite> CurrentSheet => _currentSheet;
    public Sprite CurrentSprite => GetCurrentSprite();
    public Sprite LastSprite { get; private set; }

    public void Update()
    {
        if( !_playing || _currentSheet == null || _currentSheet.Count == 0 )
            return;

        if( EditorApplication.timeSinceStartup - _lastTime > 1.0 / _fps )
        {
            LastSprite = _currentSheet[_frameIndex];
            _frameIndex = ( _frameIndex + 1 ) % _currentSheet.Count;
            _lastTime = EditorApplication.timeSinceStartup;
        }
    }

    public void SetCurrentSpriteSheet( List<Sprite> sheet )
    {
        _frameIndex = Mathf.Clamp( _frameIndex, 0, _currentSheet.Count - 1 );
        _currentSheet = sheet;
    }

    public void Clear()
    {
        _currentSheet.Clear();
    }

    public void Play()
    {
        _playing = true;
    }

    public void Pause()
    {
        _playing = false;
    }

    private Sprite GetCurrentSprite()
    {
        if( _currentSheet == null || _currentSheet.Count == 0 )
            return null;

        if( _frameIndex < 0 || _frameIndex >= _currentSheet.Count )
            _frameIndex = 0;

        return _currentSheet[_frameIndex];
    }
}
