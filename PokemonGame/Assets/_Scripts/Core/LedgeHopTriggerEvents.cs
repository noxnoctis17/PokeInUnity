using UnityEngine;

public class LedgeHopTriggerEvents : MonoBehaviour
{
    [SerializeField] LedgeHop _ledgeHop;

    private void OnTriggerEnter( Collider col ){
        float dir = Vector3.Dot( PlayerReferences.Instance.transform.forward, transform.forward );
        
        if( col.CompareTag( "Player" ) && dir < -0.95 ){
            _ledgeHop.OnLedgeHopTrigger?.Invoke( gameObject );
        }
    }

    private void OnTriggerStay( Collider col ){
        float dir = Vector3.Dot( PlayerReferences.Instance.transform.forward, transform.forward );
        
        if( col.CompareTag( "Player" ) && dir < -0.95 ){
            _ledgeHop.OnLedgeHopTrigger?.Invoke( gameObject );
        }
    }
}
