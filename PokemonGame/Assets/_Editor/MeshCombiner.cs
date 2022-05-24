using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent( typeof( MeshFilter ), typeof( MeshRenderer ) )]
public class MeshCombiner : MonoBehaviour
{
    [SerializeField] private GameObject _parentObject;
    [SerializeField] private MeshFilter[] _sourceMeshes;
    [SerializeField] private MeshFilter _mergedMesh;

    [ContextMenu( itemName: "Gather Meshes") ]
    private void GatherMeshes(){
        _sourceMeshes = _parentObject.GetComponentsInChildren<MeshFilter>();
    }

    [ContextMenu( itemName: "Combine Meshes") ]
    private void CombineMeshes(){
        var combine = new CombineInstance[ _sourceMeshes.Length ];

        for( int i = 0; i < _sourceMeshes.Length; i++ ){
            combine[i].mesh = _sourceMeshes[i].sharedMesh;
            combine[i].transform = _sourceMeshes[i].transform.localToWorldMatrix;
        }

        var mesh = new Mesh();
        mesh.CombineMeshes( combine );
        _mergedMesh.mesh = mesh;
    }
}
