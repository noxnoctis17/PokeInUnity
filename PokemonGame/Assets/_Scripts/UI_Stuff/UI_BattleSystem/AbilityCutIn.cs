using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;
using UnityEngine.UI;

public class AbilityCutIn : MonoBehaviour
{
    [SerializeField] private RectTransform _rightCutIn;
    [SerializeField] private Image _rightCutInBG;
    [SerializeField] private Image _rightCutInOL;
    [SerializeField] private Image _rightCutInPortrait;
    [SerializeField] private RectTransform _leftCutIn;
    [SerializeField] private Image _leftCutInBG;
    [SerializeField] private Image _leftCutInOL;
    [SerializeField] private Image _leftCutInPortrait;
    [SerializeField] private TextMeshProUGUI _abilityNameRight;
    [SerializeField] private TextMeshProUGUI _userNameRight;
    [SerializeField] private TextMeshProUGUI _abilityNameLeft;
    [SerializeField] private TextMeshProUGUI _userNameLeft;

    public IEnumerator CutIn( Pokemon pokemon, CourtLocation location ){
        PlayerReferences.Instance.PlayerController.DisableBattleControls();
        yield return new WaitForEndOfFrame();

        string user = pokemon.NickName;
        string ability = pokemon.Ability?.Name;
        var colors = TypeColorsDB.GetColors( pokemon );

        if( location == CourtLocation.TopCourt )
        {
            _abilityNameRight.text = ability;
            _userNameRight.text = $"{user}'s";
            _rightCutInOL.color = colors.color2;
            _rightCutInBG.color = colors.color1;
            _rightCutInPortrait.sprite = pokemon.PokeSO.CardPortrait;
            yield return AnimateAbilityCutIn( _rightCutIn, location );
        }
        else
        {
            _abilityNameLeft.text = ability;
            _userNameLeft.text = $"{user}'s";
            _leftCutInOL.color = colors.color2;
            _leftCutInBG.color = colors.color1;
            _leftCutInPortrait.sprite = pokemon.PokeSO.CardPortrait;
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
