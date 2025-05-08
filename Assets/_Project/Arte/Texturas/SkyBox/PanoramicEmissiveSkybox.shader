Shader "Custom/PanoramicEmissiveSkybox"
{
    Properties
    {
        _MainTex ("Spherical (HDR)", 2D) = "black" {}
        _EmissionMask ("Emission Mask", 2D) = "black" {}
        [HDR]_EmissionColor ("Emission Color", Color) = (1,1,1,1)
        _Exposure("Exposure", Range(0, 8)) = 1.0
        _Rotation("Rotation", Range(0, 360)) = 0
        [KeywordEnum(6 Frames Layout, Latitude Longitude Layout)] _Mapping("Mapping", Float) = 1
        [Enum(360 Degrees, 0, 180 Degrees, 1)] _ImageType("Image Type", Float) = 0
        [Toggle] _MirrorOnBack("Mirror on Back", Float) = 0
        [Enum(None, 0, Side by Side, 1, Over Under, 2)] _Layout("3D Layout", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _EmissionMask;
            float4 _MainTex_ST;
            float4 _EmissionColor;
            float _Exposure;
            float _Rotation;
            float _Mapping;
            float _ImageType;
            float _MirrorOnBack;
            float _Layout;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 texcoord : TEXCOORD0;
            };

            // Declaramos la función de rotación primero
            float3 RotateAroundYInDegrees(float3 vertex, float degrees)
            {
                float alpha = degrees * UNITY_PI / 180.0;
                float sina, cosa;
                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float3(mul(m, vertex.xz), vertex.y).xzy;
            }

            float2 ToRadialCoords(float3 coords)
            {
                float3 normalizedCoords = normalize(coords);
                float latitude = acos(normalizedCoords.y);
                float longitude = atan2(normalizedCoords.z, normalizedCoords.x);
                float2 sphereCoords = float2(longitude, latitude) * float2(0.5/UNITY_PI, 1.0/UNITY_PI);
                return float2(0.5,1.0) - sphereCoords;
            }

            v2f vert (appdata v)
            {
                v2f o;
                float3 rotated = RotateAroundYInDegrees(v.texcoord, _Rotation);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = rotated;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = ToRadialCoords(i.texcoord);
                
                if (_Layout == 0) // None
                {
                    uv = uv;
                }
                else if (_Layout == 1) // Side by Side
                {
                    uv.x *= 0.5;
                }
                else if (_Layout == 2) // Over Under
                {
                    uv.y *= 0.5;
                }

                float4 baseColor = tex2D(_MainTex, uv);
                float4 emission = tex2D(_EmissionMask, uv) * _EmissionColor;
                
                float4 finalColor = baseColor + emission;
                finalColor.rgb *= _Exposure;
                
                return finalColor;
            }
            ENDCG
        }
    }
}