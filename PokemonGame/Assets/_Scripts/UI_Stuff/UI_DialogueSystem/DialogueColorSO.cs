using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu( menuName = "Dialogue/DialogueColor")]
public class DialogueColorSO : ScriptableObject
{
    [SerializeField] private Color _trim;
    [SerializeField] private Color _inside;

    public Color Trim => _trim;
    public Color Inside => _inside;
}
