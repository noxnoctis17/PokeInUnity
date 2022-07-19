using UnityEngine;
using TMPro;
using Unity.Mathematics;

public class DamageTakenPopup : MonoBehaviour
{
    private TextMeshPro _damageTextPopup;

    public static DamageTakenPopup Create( Transform damageTakenPopupPrefab, int damageTaken, Vector3 position){
        Transform damageTakenTransform = Instantiate( damageTakenPopupPrefab, position, quaternion.identity );
        DamageTakenPopup damageTakenPopup = damageTakenTransform.GetComponent<DamageTakenPopup>();
        damageTakenPopup.Setup( damageTaken );

        return damageTakenPopup;
    }

    private void Awake(){
        _damageTextPopup = transform.GetComponent<TextMeshPro>();
    }

    public void Setup( int damageTaken ){
        _damageTextPopup.SetText( damageTaken.ToString() );
    }
}
