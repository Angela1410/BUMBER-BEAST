// Unlit sprite shader for glow effects (enemy corruption, barrier and launcher energy).
//   _Silhouette 1 = draw only the sprite's shape in the tint colour (an outline/aura copy).
//   _Additive   0 = normal alpha blending, 1 = fully additive (premultiplied blend in between).
// Uses Unity's standard sprite vertex program, so SpriteRenderer colour and flipX/flipY work.
Shader "BumperBeast/SpriteGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Additive ("Additive", Range(0, 1)) = 0
        _Silhouette ("Silhouette", Range(0, 1)) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment GlowFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            half _Additive;
            half _Silhouette;

            fixed4 GlowFrag(v2f IN) : SV_Target
            {
                fixed4 tex = SampleSpriteTexture(IN.texcoord);
                fixed3 rgb = lerp(tex.rgb, fixed3(1, 1, 1), _Silhouette) * IN.color.rgb;
                half alpha = tex.a * IN.color.a;
                return fixed4(rgb * alpha, alpha * (1 - _Additive));
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
