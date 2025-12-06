Shader "Unlit/s_ImageGradientMask"
{
    Properties
    {
        _MainTex ( "Texture", 2D ) = "white" {}
        _Color1 ( "Color 1", Color ) = ( 1, 1, 1, 0 )
        _Color2 ( "Color 2", Color ) = ( 0, 0, 0, 0 )
        _Color1_Amount ( "Color 1 Amount", Float ) = 0.000000
        _Color2_Amount ( "Color 2 Amount", Float ) = 1.000000
        _Scale ( "Scale", Float ) = 1.690000
        [ToggleUI]  _IsVertical ( "IsVertical", Float ) = 1.000000
        _Stencil("Stencil ID", Float) = 1
        _StencilComp("Stencil Comparison", Float) = 8  // 8 = Equal
        _StencilOp("Stencil Operation", Float) = 0         // Keep
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _ColorMask( "ColorMask", Float ) = 15
    }
    SubShader
    {
        Tags {
            "RenderPipeline"    = "UniversalPipeline"
            "RenderType"        = "Transparent"
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
        }

        LOD 100

        Pass
        {
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
            }
            
            ColorMask[_ColorMask]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D( _MainTex );
            SAMPLER( sampler_MainTex );
            float4 _Color1;
            float4 _Color2;

            float _Color1_Amount;
            float _Color2_Amount;
            float _Scale;
            bool _IsVertical;

            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert( appdata v )
            {
                v2f output = (v2f)0;
                
                output.positionCS = TransformObjectToHClip( v.positionOS.xyz );
                output.uv = v.uv;

                return output;
            }

            float4 frag( v2f i ) : SV_TARGET
            {
                float coord = _IsVertical ? i.uv.y : i.uv.x;

                coord *= _Scale;

                float t = smoothstep( _Color1_Amount, _Color2_Amount, coord );

                float4 gradient = lerp( _Color1, _Color2, t );

                float4 texCol = SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, i.uv );
                gradient *= texCol;

                return gradient;
            }

            ENDHLSL
        }
    }
}
