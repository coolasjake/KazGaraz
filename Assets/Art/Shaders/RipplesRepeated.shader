Shader "Ripples/RipplesRepeated" {
	//Note: Can use the tangent data of the mesh to give colour (x/y/z).
    Properties { // input data
        [PerRendererData]
        _MainTex ("Texture", 2D ) = ""
        _WaveFreq ("Wave Frequency", float ) = 10
        _WaveAmp ("Wave Amplitude", float ) = 0.02
        _WaveSpeed ("Wave Speed", float ) = 0.2
        _WaveOffset ("Wave Offset", float) = -0.05
        _RippleDur ("Waves Duration", float ) = 10
    }
    SubShader {
        Tags {
            "RenderType"="Transparent" // tag to inform the render pipeline of what type this is
            "Queue"="Transparent" // changes the render order
        }
        Pass {
            // pass tags
            
            Cull Off
            ZWrite Off
            Blend One One // additive
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            #define TAU 6.28318530718
			#define RIPPLESIZE 1

            
            float _WaveFreq;
            float _WaveAmp;
            float _WaveSpeed;
            float _WaveOffset;
            float _RippleDur;

            // automatically filled out by Unity
            struct MeshData { // per-vertex mesh data
                float4 vertex : POSITION; // local space vertex position
                float3 normals : NORMAL; // local space normal direction
                float4 tangent : TANGENT; // tangent direction (xyz) tangent sign (w)
                // float4 color : COLOR; // vertex colors
                float4 uv0 : TEXCOORD0; // uv0 diffuse/normal map textures
                //float4 uv1 : TEXCOORD1; // uv1 coordinates lightmap coordinates
                //float4 uv2 : TEXCOORD2; // uv2 coordinates lightmap coordinates
                //float4 uv3 : TEXCOORD3; // uv3 coordinates lightmap coordinates
            };

            // data passed from the vertex shader to the fragment shader
            // this will interpolate/blend across the triangle!
            struct Interpolators {
                float4 vertex : SV_POSITION; // clip space position
                float3 normal : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 tanData : TANGENT;
            };

            float HeightMultGraph (float x) {
                //Value goes from 1 to 0, but is 0 when 0 > x > 1
                return (1 - x) * (x > 0) * (x < 1);
            }

            float GetWave( float2 uv, float time ) {
                float2 uvsCentered = uv * 2 - 1; 
                float radialDistance = length( uvsCentered );
                float wave = cos( (radialDistance - _Time.y * _WaveSpeed) * TAU * _WaveFreq) * 0.5 + 0.5;
                wave *= 1-radialDistance;
                float normalizedTime = (_Time.y % _RippleDur) / _RippleDur;
                wave *= HeightMultGraph((2 * normalizedTime) - radialDistance + _WaveOffset) * RIPPLESIZE;
                wave *= radialDistance < 1;
				return wave;
            }

            Interpolators vert( MeshData v ){
                Interpolators o;

                //v.vertex.y = GetWave( v.uv0, v.tangent.w ) * _WaveAmp;
                
                o.vertex = UnityObjectToClipPos( v.vertex ); // local space to clip space
                o.normal = UnityObjectToWorldNormal( v.normals );
                o.uv = v.uv0; //(v.uv0 + _Offset) * _Scale; // passthrough
                o.tanData = v.tangent;
                return o;
            }

            float InverseLerp( float a, float b, float v ) {
                return (v-a)/(b-a);
            }

            float4 frag( Interpolators i ) : SV_Target {
                //return float4(1 * i.tanData.x, 1 * i.tanData.y, 1 * i.tanData.z, 1) * (GetWave( i.uv, i.tanData.w) / RIPPLESIZE);
                return float4(1, 1, 1, 1) * (GetWave( i.uv, i.tanData.w) / RIPPLESIZE);
                return GetWave( i.uv, i.tanData.w );
            }
            
            ENDCG
        }
    }
}
