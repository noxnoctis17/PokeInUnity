using Unity.VisualScripting;
using UnityEngine;

public static class ExtensionMethods
{
    public static Vector3 Round( this Vector3 v ){
        v.x = Mathf.Round( v.x );
        v.y = Mathf.Round( v.y );
        v.z = Mathf.Round( v.z );

        return v;
    }

    public static Vector3 Round( this Vector3 v, float size ){
        return ( v / size ).Round() * size;
    }

    public static Vector3 RoundXZ( this Vector3 v, float size ){
        v.x = ( v.x / size ).RoundX() * size;
        v.z = ( v.z / size ).RoundZ() * size;

        return v;
    }

    public static Vector3 RoundX( this Vector3 v ){
        v.x = Mathf.Round( v.x );

        return v;
    }
    public static Vector3 RoundY( this Vector3 v ){
        v.y = Mathf.Round( v.y );

        return v;
    }
    public static Vector3 RoundZ( this Vector3 v ){
        v.z = Mathf.Round( v.z );

        return v;
    }

    public static float RoundX( this float x ){
        x = Mathf.Round( x );

        return x;
    }

    public static float RoundY( this float y ){
        y = Mathf.Round( y );

        return y;
    }

    public static float RoundZ( this float z ){
        z = Mathf.Round( z );

        return z;
    }

    public static Vector3 MovementAxisCorrection( this Vector3 q, Transform cameraTransform ){
        q = Quaternion.AngleAxis( cameraTransform.rotation.eulerAngles.y, Vector3.up ) * q;
        
        return q;
    }

    public static Vector3 UndoMovementAxisCorrection( this Vector3 q, Transform cameraTransform ){
        q = Quaternion.AngleAxis( cameraTransform.rotation.eulerAngles.y, Vector3.up ) * -q;
        
        return q;
    }

}
