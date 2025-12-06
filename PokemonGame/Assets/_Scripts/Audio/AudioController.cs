using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Audio;

public class AudioController : MonoBehaviour
{
    public static AudioController Instance;
    public bool IsPlayingSFX { get; private set; }
    [SerializeField] private MusicTheme _lastOverworldTheme;
    public MusicTheme LastOverworldTheme => _lastOverworldTheme;
    [SerializeField] private AudioSource _sfxSource;
    [SerializeField] private AudioSource _overworldTheme;
    [SerializeField] private AudioSource _battleTheme;
    [SerializeField] private AudioClip _battleThemeDefault;
    [SerializeField] private AudioClip _routeMainThemeCalm;
    [SerializeField] private AudioClip _ledgeHop;
    [SerializeField] private AudioClip _bump;
    [SerializeField] private AudioClip _sendPokemon;
    [SerializeField] private AudioClip _catchSuccess;
    [SerializeField] private AudioClip _evolutionSuccess;
    [SerializeField] private AudioClip _buttonSelect;
    [SerializeField] private AudioClip _damageEffective;
    [SerializeField] private AudioClip _damageSuperEffective;
    [SerializeField] private AudioClip _damageNotEffective;
    [SerializeField] private AudioClip _battleBallThrow;
    [SerializeField] private AudioClip _battleBallDrop;
    [SerializeField] private AudioClip _battleBallShake;
    [SerializeField] private AudioClip _battleBallClick;
    [SerializeField] private AudioClip _itemGet;

    private void OnEnable(){
        if( Instance != null )
            Destroy( Instance );

        Instance = this;

        _overworldTheme.clip = _routeMainThemeCalm;
        _battleTheme.clip = _battleThemeDefault;
        _overworldTheme.volume = 1f;
        _battleTheme.volume = 0.3f;
        _overworldTheme.loop = true;
        _battleTheme.loop = true;
        PlayMusic( MusicTheme.RouteMainTheme_Calm );
    }

    public void PlayMusic( MusicTheme theme, float crossfadeDuration = 2f )
    {
        AudioClip sound = null;

        switch( theme )
        {
            case MusicTheme.BattleThemeDefault:
                sound = _battleThemeDefault;
            break;

            case MusicTheme.RouteMainTheme_Calm:
                sound = _routeMainThemeCalm;
                _lastOverworldTheme = MusicTheme.RouteMainTheme_Calm;
            break;
        }

        if( sound == _battleThemeDefault && _overworldTheme.isPlaying )
        {
            Debug.Log( "Battle Theme Music!" );
            StartCoroutine( MusicThemeCrossfade( _overworldTheme, _battleTheme, crossfadeDuration ) );
        }
        else if( sound == _routeMainThemeCalm && _battleTheme.isPlaying )
        {
            StartCoroutine( MusicThemeCrossfade( _battleTheme, _overworldTheme, crossfadeDuration ) );
        }
        else if( sound == _routeMainThemeCalm )
        {
            _overworldTheme.clip = sound;
            _overworldTheme.Play();
        }
    }

    private IEnumerator MusicThemeCrossfade( AudioSource from, AudioSource to, float duration )
    {
        to.volume = 0;
        to.Play();

        float time = 0f;

        while( time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            if( from == _battleTheme )
                from.volume = Mathf.Lerp( 0.3f, 0, t );
            else
                from.volume = Mathf.Lerp( 1f, 0, t );

            if( to == _battleTheme )
                to.volume = Mathf.Lerp( 0, 0.3f, t );
            else
                to.volume = Mathf.Lerp( 0, 1f, t );

            yield return null;
        }

        if( from == _routeMainThemeCalm )
            from.Pause();
        else
            from.Stop();

        if( from == _battleTheme )
            from.volume = 0.3f;
        else
            from.volume = 1f;
    }

    public void PlaySFX( SoundEffect effect ){
        AudioClip sound = null;

        if( IsPlayingSFX && effect == SoundEffect.Bump )
            return;

        switch( effect )
        {
            case SoundEffect.LedgeHop:
                sound = _ledgeHop;
            break;

            case SoundEffect.Bump:
                sound = _bump;
            break;

            case SoundEffect.SendPokemon:
                sound = _sendPokemon;
            break;

            case SoundEffect.CatchSuccess:
                sound = _catchSuccess;
            break;

            case SoundEffect.EvolutionSuccess:
                sound = _evolutionSuccess;
            break;

            case SoundEffect.ButtonSelect:
                sound = _buttonSelect;
            break;

            case SoundEffect.DamageEffective:
                sound = _damageEffective;
            break;

            case SoundEffect.DamageSuperEffective:
                sound = _damageSuperEffective;
            break;

            case SoundEffect.DamageNotEffective:
                sound = _damageNotEffective;
            break;

            case SoundEffect.BattleBallThrow:
                sound = _battleBallThrow;
            break;

            case SoundEffect.BattleBallDrop:
                sound = _battleBallDrop;
            break;

            case SoundEffect.BattleBallShake:
                sound = _battleBallShake;
            break;

            case SoundEffect.BattleBallClick:
                sound = _battleBallClick;
            break;

            case SoundEffect.ItemGet:
                sound = _itemGet;
            break;
        }

        IsPlayingSFX = true;
        if( sound != null )
            StartCoroutine( PlaySFX( sound ) );
    }

    private IEnumerator PlaySFX( AudioClip sound ){
        _sfxSource.PlayOneShot( sound );
        if( sound == _bump)
        {
            yield return new WaitWhile( () => _sfxSource.isPlaying );
        }

        IsPlayingSFX = false;
    }

}

public enum SoundEffect {
    LedgeHop,
    Bump,
    SendPokemon,
    CatchSuccess,
    EvolutionSuccess,
    ButtonSelect,
    DamageEffective,
    DamageSuperEffective,
    DamageNotEffective,
    BattleBallThrow,
    BattleBallDrop,
    BattleBallShake,
    BattleBallClick,
    ItemGet,

    }

public enum MusicTheme { BattleThemeDefault, RouteMainTheme_Calm }
