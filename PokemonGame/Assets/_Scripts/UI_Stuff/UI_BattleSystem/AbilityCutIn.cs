using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;

public class AbilityCutIn : MonoBehaviour
{
    [SerializeField] private RectTransform _rightCutIn;
    [SerializeField] private RectTransform _leftCutIn;
    [SerializeField] private TextMeshProUGUI _abilityNameRight;
    [SerializeField] private TextMeshProUGUI _userNameRight;
    [SerializeField] private TextMeshProUGUI _abilityNameLeft;
    [SerializeField] private TextMeshProUGUI _userNameLeft;

    public IEnumerator CutIn( string ability, string user, CourtLocation location ){
        PlayerReferences.Instance.PlayerController.DisableBattleControls();
        yield return new WaitForEndOfFrame();

        if( location == CourtLocation.TopCourt )
        {
            _abilityNameRight.text = ability;
            _userNameRight.text = user;
            yield return AnimateAbilityCutIn( _rightCutIn, location );
        }
        else
        {
            _abilityNameLeft.text = ability;
            _userNameLeft.text = user;
            yield return AnimateAbilityCutIn( _leftCutIn, location );
        }

        yield return new WaitForSeconds( 0.1f );
        PlayerReferences.Instance.PlayerController.EnableBattleControls();
    }

    //---------------------------ANIMATION SWIPE IN-------------------------------

    private IEnumerator AnimateAbilityCutIn( RectTransform rect, CourtLocation location ){
        var sequence = DOTween.Sequence();

        if( location == CourtLocation.TopCourt )
        {
            sequence.Append( rect.DOAnchorPosX( -10f, 0.2f ) ).WaitForCompletion();
            sequence.Append( rect.DOAnchorPosX( 0f, 0.2f ) ).WaitForCompletion();
            sequence.AppendInterval( 1f );
            sequence.Append( rect.DOAnchorPosX( 550f, 0.2f ) ).WaitForCompletion();
        }
        else
        {
            sequence.Append( rect.DOAnchorPosX( 10f, 0.2f ) ).WaitForCompletion();
            sequence.Append( rect.DOAnchorPosX( 0f, 0.2f ) ).WaitForCompletion();
            sequence.AppendInterval( 1f );
            sequence.Append( rect.DOAnchorPosX( -550f, 0.2f ) ).WaitForCompletion();
        }

        yield return sequence.WaitForCompletion();
    }
}
