using UnityEngine;

public class DontDestroyOn : MonoBehaviour
{
    private void Awake(){
        DontDestroyOnLoad( gameObject );
    }
}
