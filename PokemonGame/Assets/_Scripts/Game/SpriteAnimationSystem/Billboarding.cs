using UnityEditor;
using UnityEngine;

// [ExecuteAlways]
public class Billboarding : MonoBehaviour
{
    private Transform _cameraTransform;

    private void Start(){
        _cameraTransform = PlayerReferences.MainCameraTransform;
    }

    private void LateUpdate(){
        transform.forward = _cameraTransform.forward;
    }

// #if UNITY_EDITOR
//     private void OnDrawGizmos(){
//         if( !Application.isPlaying )
//             transform.forward = SceneView.lastActiveSceneView.camera.transform.forward;
//     }
// #endif

}
