using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UI_ScaleFixer : MonoBehaviour
{
    [SerializeField] private Vector3 _scale;

    private void Update()
    {
        if( transform.localScale != _scale )
            transform.localScale = _scale;
    } 
}
