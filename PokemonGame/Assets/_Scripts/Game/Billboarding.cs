using UnityEditor;
using UnityEngine;

public class Billboarding : MonoBehaviour
{
    private Transform _cameraTransform;
    private Quaternion _spriteRotation;



    private void Start(){
        _cameraTransform = PlayerReferences.MainCameraTransform;
        _spriteRotation = transform.rotation;
    }

    private void LateUpdate(){
            transform.forward = _cameraTransform.forward;
    }

}
