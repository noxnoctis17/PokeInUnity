using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridObject : MonoBehaviour
{
    [SerializeField] private Material _previewMaterial;
    public Material PreviewMaterial => _previewMaterial;
}
