using UnityEngine;
using UnityEngine.Events;
using System;

[Serializable]
public class ResponseEvent
{
    [HideInInspector] public string name;
    [SerializeField] private UnityEvent _onPickedResponse;
    public UnityEvent OnPickedResponse => _onPickedResponse;

}
