using UnityEngine;

public class LightReferences : MonoBehaviour
{
    public static LightReferences Instance { get; private set; }
    [SerializeField] private Transform _sunTransform;
    [SerializeField] private Transform _moonTransform;
    public Transform SunTransform => _sunTransform;
    public Transform MoonTransform => _moonTransform;

    private void OnEnable(){
        Instance = this;
    }
}
