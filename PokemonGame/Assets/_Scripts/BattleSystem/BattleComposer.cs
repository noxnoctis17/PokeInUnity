using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Analytics;

public class BattleComposer : MonoBehaviour
{
    [SerializeField] private CinemachineVirtualCamera _lookAtOpponent;
    [SerializeField] private CinemachineVirtualCamera _lookAtPlayer;
    [SerializeField] private CinemachineVirtualCamera _followAttacker;
    [SerializeField] private CinemachineVirtualCamera _followAttacker_LookAt;
    [SerializeField] private BattleSystem _battleSystem;
    private CinemachineBrain _cmBrain;
    public CinemachineBrain CMBrain => _cmBrain;
    private float _cameraDelay = 0.25f;

    public void Init( CinemachineBrain cmBrain)
    {
        _cmBrain = cmBrain;
    }

    private IEnumerator FollowAttacker( Transform attacker )
    {
        _followAttacker.LookAt = attacker;
        _followAttacker.Follow = attacker;
        _followAttacker.gameObject.SetActive( true );
        yield return null;
        yield return new WaitUntil( () => !_cmBrain.IsBlending );
    }

    private IEnumerator FollowAttacker_LookAt( Transform attacker )
    {
        _followAttacker_LookAt.LookAt = attacker;
        _followAttacker_LookAt.Follow = attacker;
        _followAttacker_LookAt.gameObject.SetActive( true );
        yield return null;
        yield return new WaitUntil( () => !_cmBrain.IsBlending );
    }

    private IEnumerator LookAt( Transform target )
    {
        _lookAtPlayer.LookAt = target.transform;
        _lookAtPlayer.Follow = target.transform;
        _lookAtPlayer.gameObject.SetActive( true );
        yield return new WaitUntil( () => !_cmBrain.IsBlending );
    }

    private void ClearCamera( CinemachineVirtualCamera camera )
    {
        camera.gameObject.SetActive( false );
        camera.LookAt = null;
        camera.Follow = null;
    }

    private void ClearCamera( CinemachineFreeLook camera )
    {
        camera.gameObject.SetActive( false );
        camera.LookAt = null;
        camera.Follow = null;
    }

    private IEnumerator WaitForCoroutine( WaitUntil waitUntil, Coroutine coroutine )
    {
        yield return waitUntil;
        yield return coroutine;
    }

    public IEnumerator RunMoveToAttackPosition( Move move, BattleUnit attacker, BattleUnit target )
    {
        switch( move.MoveSO.AnimationType )
        {
            //--None
            case AnimationType.None:
                yield return null;
            break;
            
            //--Strike
            case AnimationType.Strike:
                yield return attacker.PokeAnimator.PlayMoveIntoStrikePosition( target.PokeTransform );
            break;

            //--Shoot
            case AnimationType.Shoot:
                yield return attacker.PokeAnimator.PlayMoveIntoShootPosition( target.PokeTransform );
            break;

            //--Status
            case AnimationType.Status:
                yield return null;
            break;

            //--Dance
            case AnimationType.Dance:
                yield return null;
            break;

            //--Earthquake
            case AnimationType.Earthquake:
                yield return null;
            break;

            //--Fake Out
            case AnimationType.FakeOut:
                yield return null;
            break;

            //--Fast
            case AnimationType.Fast:
                yield return null;
            break;

            //--Pivot
            case AnimationType.Pivot:
                yield return null;
            break;
        }
    }

    public IEnumerator RunAttackAnimation( Move move, BattleUnit attacker, BattleUnit target )
    {
        switch( move.MoveSO.AnimationType )
        {
            //--None
            case AnimationType.None:
                yield return null;
            break;
            
            //--Strike
            case AnimationType.Strike:
                yield return RunStrikeAttackScene( move, attacker, target );
            break;

            //--Shoot
            case AnimationType.Shoot:
                yield return RunShootAttackScene( move, attacker, target );
            break;

            //--Status
            case AnimationType.Status:
                yield return null;
            break;

            //--Dance
            case AnimationType.Dance:
                yield return null;
            break;

            //--Earthquake
            case AnimationType.Earthquake:
                yield return null;
            break;

            //--Fake Out
            case AnimationType.FakeOut:
                yield return RunFakeOutAttackScene( move, attacker, target );
            break;

            //--Fast
            case AnimationType.Fast:
                yield return RunFastAttackScene( move, attacker, target );
            break;

            //--Pivot
            case AnimationType.Pivot:
                yield return RunPivotAttackScene( move, attacker, target );
            break;

        }
    }

    public IEnumerator RunStatusAttackScene( Move move, BattleUnit attacker, BattleUnit target )
    {
        yield return attacker.PokeAnimator.PlayStatusAttackAnimation();
    }

    public IEnumerator RunStrikeAttackScene( Move move, BattleUnit attacker, BattleUnit target )
    {
        TweenCallback cameraCallback = null;
        
        if( GameSettings.Instance.UseBattleCameras )
        {
            cameraCallback = () =>
            {
                WaitUntil wait = new( () => !_cmBrain.IsBlending );
                Coroutine coroutine = StartCoroutine( LookAt( target.PokeTransform ) );

                ClearCamera( _followAttacker );
                ClearCamera( _followAttacker_LookAt );
                StartCoroutine( WaitForCoroutine( wait, coroutine ) );
            };

            //--Choose between behind camera or facing camera for dash up to target animation
            int coin = Random.Range( 1, 4 );
            if( coin == 1 )
                yield return FollowAttacker( attacker.PokeTransform );
            else if( coin == 2 )
                yield return FollowAttacker_LookAt( attacker.PokeTransform );
            else if( coin == 3 )
            {
                cameraCallback = null;
                yield return null;
            }

            yield return _cameraDelay;
        }

        yield return attacker.PokeAnimator.PlayStrikeAnimation( target.PokeTransform, cameraCallback );
    }

    public IEnumerator RunShootAttackScene( Move move, BattleUnit attacker, BattleUnit target )
    {
        yield return _cameraDelay;
        yield return attacker.PokeAnimator.PlayShootAnimation( target.PokeTransform );
    }

    public IEnumerator RunFakeOutAttackScene( Move move, BattleUnit attacker, BattleUnit target )
    {
        yield return _cameraDelay;
        yield return attacker.PokeAnimator.PlayFakeOutAnimation( target.PokeTransform );
    }

    public IEnumerator RunFastAttackScene( Move move, BattleUnit attacker, BattleUnit target )
    {
        yield return _cameraDelay;
        yield return attacker.PokeAnimator.PlayFastAnimation( target.PokeTransform );
    }

    public IEnumerator RunPivotAttackScene( Move move, BattleUnit attacker, BattleUnit target )
    {
        yield return _cameraDelay;
        yield return attacker.PokeAnimator.PlayPivotAnimation( target.PokeTransform );
    }

    public IEnumerator RunTakeDamagePhase( float typeEffectiveness, BattleUnit target )
    {
        if( typeEffectiveness == 1 )
            AudioController.Instance.PlaySFX( SoundEffect.DamageEffective );
        else if( typeEffectiveness > 1 )
            AudioController.Instance.PlaySFX( SoundEffect.DamageSuperEffective );
        else if( typeEffectiveness < 1 )
            AudioController.Instance.PlaySFX( SoundEffect.DamageNotEffective );

        yield return target.PokeAnimator.PlayTakeDamageAnimation();
        ClearCamera( _lookAtPlayer );
    }
}
