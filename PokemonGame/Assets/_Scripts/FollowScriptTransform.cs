using UnityEngine;

public class FollowScriptTransform : MonoBehaviour
{
    [SerializeField] private Transform _scriptsTransform;
    private void Update(){
        transform.position = _scriptsTransform.position;
    }
}
