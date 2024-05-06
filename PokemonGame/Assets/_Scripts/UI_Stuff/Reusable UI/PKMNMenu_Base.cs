using NoxNoctisDev.StateMachine;
using System;
using UnityEngine;

public class PKMNMenu_Base<T> : State<T>
{
    [SerializeField] protected PKMNMenu_Events PKMNMenu_Events;
}
