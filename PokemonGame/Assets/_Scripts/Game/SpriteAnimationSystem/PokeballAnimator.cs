using System;
using UnityEngine;
using DG.Tweening;
using System.Collections;

public class PokeballAnimator : MonoBehaviour
{
    private SpriteRenderer _spriteRenderer;
    private Transform _cameraTransform;
    private bool _isAnimating;

    private void OnEnable(){
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _cameraTransform = PlayerReferences.MainCameraTransform;
    }

    private void LateUpdate(){
        if( !_isAnimating )
            transform.forward = _cameraTransform.forward;
    }

    public IEnumerator TryCaptureShake(){
        _isAnimating = true;
        yield return transform.DOPunchRotation( new Vector3( _cameraTransform.forward.x, _cameraTransform.forward.y, 15f ), 0.75f ).WaitForCompletion();
        _isAnimating = false;
    }

    public IEnumerator Fadeout( float duration, bool wait ){
        if( wait ){
            _isAnimating = true;
            yield return _spriteRenderer.DOFade( 0, duration ).WaitForCompletion();
            _isAnimating = false;
        }
        else{
            _isAnimating = true;
            _spriteRenderer.DOFade( 0, duration );
            _isAnimating = false;
        }
    }


}
