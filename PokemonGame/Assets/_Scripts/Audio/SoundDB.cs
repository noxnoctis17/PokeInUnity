using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundDB
{
    public static Dictionary<string, SoundSO> Sounds { get; private set; }

    public static void Init()
    {
        Sounds = new();

        var dbArray = Resources.LoadAll<SoundSO>( "" );
        foreach( var sound in dbArray )
        {
            var key = sound.ID;

            if( Sounds.ContainsKey( key ) )
            {
                Debug.LogError( "Duplicate Sound found round hound bound clowned" );
                continue;
            }

            Sounds[key] = sound;
        }
    }
}
