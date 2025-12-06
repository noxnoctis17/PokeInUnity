using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;

public class AbilityCutIn : MonoBehaviour
{
    [SerializeField] RectTransform _parentTransform;
    [SerializeField] TextMeshProUGUI _abilityName;
    [SerializeField] TextMeshProUGUI _userName;
    private RectTransform _rectTrans;

    private void OnEnable(){
        _rectTrans = GetComponent<RectTransform>();
    }

    public IEnumerator CutIn( string ability, string user ){
        PlayerReferences.Instance.PlayerController.DisableBattleControls();
        yield return new WaitForEndOfFrame();

        _abilityName.text = ability;
        _userName.text = user;
        yield return AnimateDialogueBox();

        yield return new WaitForSeconds( 0.1f );
        PlayerReferences.Instance.PlayerController.EnableBattleControls();
    }


    //---------------------------ANIMATION SWIPE IN-------------------------------

    private IEnumerator AnimateDialogueBox(){
        yield return _rectTrans.DOAnchorPosX( -740f, 0.2f ); //--We don't wait for completion so that it moves in conjunction with BounceIn()? I should just make these a sequence.
        yield return BounceIn();
    }

    private IEnumerator BounceIn(){
        yield return new WaitForSeconds( 0.2f );
        yield return _rectTrans.DOAnchorPosX( -750f, 0.2f ).WaitForCompletion();

        yield return HideBoxDelay();
    }

    private IEnumerator HideBoxDelay(){
        yield return new WaitForSeconds( 1f );
        yield return _rectTrans.DOAnchorPosX( -1600f, 0.2f );
    }
}
