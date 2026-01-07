using System.Collections;
using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class TypeText : MonoBehaviour
{
    [SerializeField] private float _typeSpeed;
    private TMP_Text _dialogueText;
    public bool IsRunning { get; private set; }
    private Coroutine typingCoroutine;

    private readonly List<Punctuation> _punctuations = new(){
        new Punctuation( new HashSet<char>(){ '.', '!', '?' }, 0.3f ),
        new Punctuation( new HashSet<char>(){ ',', ':', ';' }, 0.1f )

    };
    
    public void RunDialogue( string textToType, TMP_Text dialogueText ){
        IsRunning = true;
        typingCoroutine = StartCoroutine( TypeDialogue( textToType, dialogueText) );
    }

    public void StopDialogue(){
        StopCoroutine( typingCoroutine );
        _dialogueText.alpha = 255;
        IsRunning = false;
    }
    
    private IEnumerator TypeDialogue( string textToType, TMP_Text dialogueText )
    {
        _dialogueText = dialogueText;
        dialogueText.alpha = 0;
        dialogueText.text = textToType;
        dialogueText.ForceMeshUpdate();
        var textInfo = dialogueText.textInfo;

        for( int i = 0; i < textInfo.characterCount; i++)
        {
            var charInfo = textInfo.characterInfo[i];
            SetTextCharacterAlpha( textInfo, charInfo, 0 );
        }

        textInfo = dialogueText.textInfo;


        float t = 0;
        int charIndex = 0;
        
        Debug.Log( $"textToType: {textToType}" );
        while( charIndex < textToType.Length )
        {
            int lastCharIndex = charIndex;

            t += Time.deltaTime * _typeSpeed;

            charIndex = Mathf.FloorToInt( t );
            charIndex = Mathf.Clamp( charIndex, 0, textToType.Length );

            for( int i = lastCharIndex; i < charIndex; i++ )
            {
                var charInfo = textInfo.characterInfo[i];
                SetTextCharacterAlpha( textInfo, charInfo, 255 );
                dialogueText.UpdateVertexData( TMP_VertexDataUpdateFlags.Colors32 );

                if( IsPunctuation( textToType[i], out float waitTime ) )
                    yield return new WaitForSeconds( waitTime );
            }

            yield return null;
        }

        IsRunning = false;
    }

    private void SetTextCharacterAlpha( TMP_TextInfo textInfo, TMP_CharacterInfo charInfo, byte alpha )
    {
        int meshIndex = charInfo.materialReferenceIndex;
        int vertIndex = charInfo.vertexIndex;
        var colors = textInfo.meshInfo[meshIndex].colors32;

        colors[vertIndex + 0].a = alpha;
        colors[vertIndex + 1].a = alpha;
        colors[vertIndex + 2].a = alpha;
        colors[vertIndex + 3].a = alpha;
    }

    private bool IsPunctuation( char character, out float waittime ){
        foreach( Punctuation punctuationCategory in _punctuations){
            if( punctuationCategory.Punctuations.Contains( character ) ){
                waittime = punctuationCategory.WaitTime;
                return true;
            }
        }
        waittime = default;
        return false;
    }

    private readonly struct Punctuation
    {
        public readonly HashSet<char> Punctuations;
        public readonly float WaitTime;

        public Punctuation( HashSet<char> punctuations, float waitTime )
        {
            Punctuations = punctuations;
            WaitTime = waitTime;
        }
    }
}
