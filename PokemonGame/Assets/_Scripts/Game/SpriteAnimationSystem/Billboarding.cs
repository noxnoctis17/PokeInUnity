using UnityEditor;
using UnityEngine;

public class Billboarding : MonoBehaviour
{
    private Transform _cameraTransform;

    private void Start(){
        _cameraTransform = PlayerReferences.MainCameraTransform;
    }

    private void LateUpdate(){
        transform.forward = _cameraTransform.forward;
    }

}
