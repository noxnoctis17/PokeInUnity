using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleDialogueBox : MonoBehaviour
{
    [SerializeField] RectTransform _dialogueBoxParent;
    [SerializeField] TextMeshProUGUI _dialogueText;
    private PlayerInput _playerInput;

    private void OnEnable(){
        _playerInput = PlayerReferences.Instance.PlayerInput;
    }

    private void AnimateDialogueBox( bool wait = false ){
        LeanTween.moveLocalX( gameObject, -370, 0.2f );
        StartCoroutine( BounceIn( wait ) );
    }

    public IEnumerator TypeDialogue( string dialogue, bool wait = false ){
        PlayerReferences.Instance.PlayerController.DisableBattleControls();
        yield return new WaitForEndOfFrame();
        
        AnimateDialogueBox( wait );
        _dialogueText.text = "";
        foreach( var letter in dialogue.ToCharArray() )
        {
            _dialogueText.text += letter;
            yield return new WaitForSeconds( 0.5f/60 );
        }

        if( wait ){
            PlayerReferences.Instance.PlayerController.EnableBattleControls();
            yield return new WaitUntil( _playerInput.UI.Submit.WasReleasedThisFrame );
            PlayerReferences.Instance.PlayerController.DisableBattleControls();
        }

        yield return new WaitForSeconds( 1f );
        PlayerReferences.Instance.PlayerController.EnableBattleControls();
    }


    //---------------------------ANIMATION SWIPE IN-------------------------------

    private IEnumerator BounceIn( bool wait = false ){
        yield return new WaitForSeconds( 0.2f );
        LeanTween.moveLocalX( gameObject, -360, 0.2f );

        if( wait )
            yield return new WaitUntil( _playerInput.UI.Submit.WasReleasedThisFrame );

        StartCoroutine( HideBoxDelay( wait ) );
    }

    private IEnumerator HideBoxDelay( bool wait = false ){
        yield return new WaitForSeconds( 1f );
        LeanTween.moveLocalX( gameObject, -1600f, 0.2f );
        yield return new WaitForSeconds( 1f );
    }
}
