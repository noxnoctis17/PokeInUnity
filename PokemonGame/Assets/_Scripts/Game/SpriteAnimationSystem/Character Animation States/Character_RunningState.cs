using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class Character_RunningState : State<CharacterAnimator>
{
    [SerializeField] private List<Sprite> _runDownSprites;
    [SerializeField] private List<Sprite> _runUpSprites;
    [SerializeField] private List<Sprite> _runLeftSprites;
    [SerializeField] private List<Sprite> _runRightSprites;
    [SerializeField] private List<Sprite> _runDownLeftSprites;
    [SerializeField] private List<Sprite> _runDownRightSprites;
    [SerializeField] private List<Sprite> _runUpLeftSprites;
    [SerializeField] private List<Sprite> _runUpRightSprites;
}
