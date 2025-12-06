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

    public IEnumerator RunStatusAttackScene( Move move, BattleUnit attacker, BattleUnit target )
    {
        yield return attacker.PokeAnimator.PlayStatusAttackAnimation();
    }

    public IEnumerator RunPhysicalAttackScene( Move move, BattleUnit attacker, BattleUnit target )
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

        yield return attacker.PokeAnimator.PlayPhysicalAttackAnimation( attacker.PokeTransform, target.PokeTransform, cameraCallback );
    }

    public IEnumerator RunSpecialAttackScene( Move move, BattleUnit attacker, BattleUnit target )
    {
        yield return _cameraDelay;
        yield return attacker.PokeAnimator.PlaySpecialAttackAnimation( target.PokeTransform );
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
