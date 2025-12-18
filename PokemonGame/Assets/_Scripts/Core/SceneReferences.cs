using UnityEngine;

public class SceneReferences : MonoBehaviour
{
    public static SceneReferences Instance { get; private set; }
    public Transform SunTransform { get; private set; }
    public Transform MoonTransform { get; private set; }

    private void Start(){
        Instance = this;
        SunTransform = LightReferences.Instance.SunTransform;
        MoonTransform = LightReferences.Instance.MoonTransform;
    }
    
}
