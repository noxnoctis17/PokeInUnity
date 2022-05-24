using UnityEngine;

public class Billboarding : MonoBehaviour
{
    [SerializeField] private Transform _cameraTransform;
    private Quaternion _rotation;

    private void Start(){
        _cameraTransform = Camera.main.transform;
        _rotation = transform.rotation;
    }

    private void LateUpdate(){
        transform.forward = _cameraTransform.forward;
    }

}
