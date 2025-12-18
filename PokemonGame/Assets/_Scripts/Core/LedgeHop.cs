using System.Collections;
using UnityEngine;
using System;

public enum TileDirection{ Up, Down, Left, Right, Center, TopLeft, TopRight, BottomLeft, BottomRight, InnerTopLeft, InnerTopRight, InnerBottomLeft, InnerBottomRight, }
public class LedgeHop : MonoBehaviour
{
    [SerializeField] private TileDirection _tileDirection;
    [SerializeField] private BoxCollider _collider;
    [SerializeField] private BoxCollider _collider2;
    [SerializeField] private BoxCollider _jumpCollider;
    [SerializeField] private BoxCollider _jumpCollider2;
    [SerializeField] private BoxCollider _triggeredCollider;
    [SerializeField] private float _x;
    [SerializeField] private float _z;
    [SerializeField] private bool _isCorner;
    public TileDirection TileDirection => _tileDirection;
    public Action<GameObject> OnLedgeHopTrigger;

    private void OnEnable(){
        OnLedgeHopTrigger += OnLedgeHop;
    }

    private void OnDisable(){
        OnLedgeHopTrigger -= OnLedgeHop;
    }

    public void Init( TileDirection direction ){
        _tileDirection = direction;

        switch( direction )
        {
            case TileDirection.Up:
                transform.rotation = Quaternion.Euler( 0f, 180f, 0f );
                _x = 0f;
                _z = 1f;
            break;

            case TileDirection.Down:
                transform.rotation = Quaternion.Euler( 0f, 0f, 0f );
                _x = 0f;
                _z = -1f;
            break;

            case TileDirection.Left:
                transform.rotation = Quaternion.Euler( 0f, 90f, 0f );
                _x = -1f;
                _z = 0f;
            break;

            case TileDirection.Right:
                transform.rotation = Quaternion.Euler( 0f, -90f, 0f );
                _x = 1f;
                _z = 0f;
            break;

            case TileDirection.TopLeft:
                transform.rotation = Quaternion.Euler( 0f, 90f, 0f );
                _isCorner = true;
            break;

            case TileDirection.TopRight:
                transform.rotation = Quaternion.Euler( 0f, 180f, 0f );
                _isCorner = true;
            break;

            case TileDirection.BottomLeft:
                transform.rotation = Quaternion.Euler( 0f, 0f, 0f );
                _isCorner = true;
            break;

            case TileDirection.BottomRight:
                transform.rotation = Quaternion.Euler( 0f, -90f, 0f );
                _isCorner = true;
            break;
        }
    }

    private void OnLedgeHop( GameObject ledgeTrigger ){
        if( _isCorner ){
            if( ledgeTrigger == _jumpCollider.gameObject ){
                _triggeredCollider = _jumpCollider;
                // Debug.Log( "Jump Collider" );
            }
            else if( _jumpCollider2 != null && ledgeTrigger == _jumpCollider2.gameObject ){
                _triggeredCollider = _jumpCollider2;
                // Debug.Log( "Jump Collider 2" );
            }
            HandleCorner();
        }
        else
            StartLedgeJumpCR();
    }

    private void HandleCorner(){
        switch( _tileDirection )
        {
            case TileDirection.TopLeft:
                if( _triggeredCollider == _jumpCollider2 ){
                    _x = 0f;
                    _z = 1f;
                }
                else if( _triggeredCollider == _jumpCollider ){
                    _x = -1f;
                    _z = 0;
                }
            break;

            case TileDirection.TopRight:
                if( _triggeredCollider == _jumpCollider2 ){
                    _x = 1f;
                    _z = 0f;
                }
                else if( _triggeredCollider == _jumpCollider ){
                    _x = 0f;
                    _z = 1f;
                }
            break;

            case TileDirection.BottomLeft:
                if( _triggeredCollider == _jumpCollider2 ){
                    Debug.Log( "BottomLeft Jump left" );
                    _x = -1f;
                    _z = 0f;
                }
                else if( _triggeredCollider == _jumpCollider ){
                    Debug.Log( "BottomLeft Jump down" );
                    _x = 0f;
                    _z = -1f;
                }
            break;

            case TileDirection.BottomRight:
                if( _triggeredCollider == _jumpCollider2 ){
                    _x = 0f;
                    _z = -1f;
                }
                else if( _triggeredCollider == _jumpCollider ){
                    _x = 1f;
                    _z = 0f;
                }
            break;
        }

        StartLedgeJumpCR();
    }

    private void StartLedgeJumpCR(){
        Vector3 destination = transform.position;
        destination.x += _x;
        destination.z += _z;
        StartCoroutine( MakeLedgeJump( destination ) );
    }

    private IEnumerator MakeLedgeJump( Vector3 destination ){
        _collider.enabled = false;
        _jumpCollider.enabled = false;

        if( _collider2 != null )
            _collider2.enabled = false;
        if( _jumpCollider2 != null )
            _jumpCollider2.enabled = false;

        yield return PlayerReferences.Instance.PlayerMovement.MakePlayerLedgeHop( destination );

        _collider.enabled = true;
        _jumpCollider.enabled = true;

        if( _collider2 != null )
            _collider2.enabled = true;
        if( _jumpCollider2 != null )
            _jumpCollider2.enabled = true;
    }

#if UNITY_EDITOR

    public void OnDrawGizmos(){
        //--LedgeHop's Forward. It should always face inward. When it's looking at the player's forward, it causes a ledge hop.
        Debug.DrawRay( new Vector3( transform.position.x, transform.position.y, transform.position.z ), transform.forward * 1f, Color.red );
        if( _jumpCollider2 != null )
        Debug.DrawRay( new Vector3( _jumpCollider2.transform.position.x, _jumpCollider2.transform.position.y, _jumpCollider2.transform.position.z ), _jumpCollider2.transform.forward * 1f, Color.red );

        //--The Collision's box, which prevents movement off or onto the ledge. Gets disabled when a Ledge Hop is triggered.
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube( _collider.bounds.center, _collider.bounds.size );
        if( _collider2 != null )
            Gizmos.DrawWireCube( _collider2.bounds.center, _collider2.bounds.size );

        //--The Ledge Hop's trigger collider.
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube( _jumpCollider.bounds.center, _jumpCollider.bounds.size );
        if( _jumpCollider2 != null )
            Gizmos.DrawWireCube( _jumpCollider2.bounds.center, _jumpCollider2.bounds.size );
    }

#endif

}
