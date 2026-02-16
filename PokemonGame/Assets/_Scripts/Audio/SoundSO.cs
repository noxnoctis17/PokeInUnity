using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu( menuName = "SoundSO" )]
public class SoundSO : ScriptableObject
{
    [SerializeField] private string _id;
    [SerializeField] private SoundType _soundType;
    [SerializeField] private float _defaultVolume = 1f;
    [SerializeField] private bool _loop;
    [SerializeField] private AudioClip _clip;

    public string ID => _id;
    public SoundType Type => _soundType;
    public float DefaultVolume => _defaultVolume;
    public bool Loop => _loop;
    public AudioClip Clip => _clip;
}

public enum SoundType { OverworldMusic, BattleTheme, SoundEffect }
