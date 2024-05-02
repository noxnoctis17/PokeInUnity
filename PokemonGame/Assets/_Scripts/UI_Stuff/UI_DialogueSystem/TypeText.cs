using System.Collections;
using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class TypeText : MonoBehaviour
{
    [SerializeField] private float _typeSpeed;
    public bool IsRunning { get; private set; }
    private Coroutine typingCoroutine;

    private readonly List<Punctuation> _punctuations = new(){
        new Punctuation( new HashSet<char>(){ '.', '!', '?' }, 0.3f ),
        new Punctuation( new HashSet<char>(){ ',', ':', ';' }, 0.1f )

    };
    
    public void RunDialogue( string textToType, TMP_Text dialogueText ){
        typingCoroutine = StartCoroutine( TypeDialogue( textToType, dialogueText) );
    }

    public void StopDialogue(){
        StopCoroutine( typingCoroutine );
        IsRunning = false;
    }
    
    private IEnumerator TypeDialogue( string textToType, TMP_Text dialogueText ){
        IsRunning = true;
        dialogueText.text = string.Empty;
        
        float t = 0;
        int charIndex = 0;
        
        while( charIndex < textToType.Length ){
            int lastCharIndex = charIndex;

            t += Time.deltaTime * _typeSpeed;

            charIndex = Mathf.FloorToInt( t );
            charIndex = Mathf.Clamp( charIndex, 0, textToType.Length );

            for( int i = lastCharIndex; i < charIndex; i++ ){
                bool isLast = i >= textToType.Length - 1;

                dialogueText.text = textToType.Substring( 0, i + 1 );

                if( IsPunctuation( textToType[i], out float waitTime ) && !isLast && !IsPunctuation( textToType[ i + 1 ], out _ ) ){
                    yield return new WaitForSeconds( waitTime );
                }
            }

            yield return null;
        }

        IsRunning = false;
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

    //--something something fuck dictionaries or whatever
    private readonly struct Punctuation{
        public readonly HashSet<char> Punctuations;
        public readonly float WaitTime;

        public Punctuation( HashSet<char> punctuations, float waitTime ){
            Punctuations = punctuations;
            WaitTime = waitTime;
        }
    }
}
