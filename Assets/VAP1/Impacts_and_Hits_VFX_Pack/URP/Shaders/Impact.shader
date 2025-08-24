// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "VAP1//Impact"
{
	Properties
	{
		_MainTexture("_MainTexture", 2D) = "white" {}
		_Noise("Noise", 2D) = "white" {}
		_Noise_Speed("Noise_Speed", Vector) = (0,0,0,0)
		_Alpha_Strength("Alpha_Strength", Float) = 1
		_Alpha_Contrast("Alpha_Contrast", Float) = 1
		_Emission_Strength("Emission_Strength", Float) = 1
		_Emission_Contrast("Emission_Contrast", Float) = 1
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] _texcoord2( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Back
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#pragma surface surf Unlit alpha:fade keepalpha noshadow 
		#undef TRANSFORM_TEX
		#define TRANSFORM_TEX(tex,name) float4(tex.xy * name##_ST.xy + name##_ST.zw, tex.z, tex.w)
		struct Input
		{
			float4 vertexColor : COLOR;
			float2 uv_texcoord;
			float4 uv2_texcoord2;
		};

		uniform sampler2D _MainTexture;
		uniform float4 _MainTexture_ST;
		uniform float _Emission_Strength;
		uniform float _Emission_Contrast;
		uniform sampler2D _Noise;
		uniform float2 _Noise_Speed;
		uniform float4 _Noise_ST;
		uniform float _Alpha_Strength;
		uniform float _Alpha_Contrast;

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float2 uv_MainTexture = i.uv_texcoord * _MainTexture_ST.xy + _MainTexture_ST.zw;
			float4 tex2DNode5 = tex2D( _MainTexture, uv_MainTexture );
			float4 saferPower27 = abs( ( ( i.vertexColor * tex2DNode5 ) * _Emission_Strength ) );
			float4 temp_cast_0 = (_Emission_Contrast).xxxx;
			o.Emission = pow( saferPower27 , temp_cast_0 ).rgb;
			float Erosion_X32 = i.uv2_texcoord2.x;
			float4 temp_cast_2 = (Erosion_X32).xxxx;
			float4 temp_cast_3 = (1.0).xxxx;
			float4 smoothstepResult7 = smoothstep( temp_cast_2 , temp_cast_3 , tex2DNode5);
			float2 uv_Noise = i.uv_texcoord * _Noise_ST.xy + _Noise_ST.zw;
			float2 panner15 = ( 1.0 * _Time.y * _Noise_Speed + uv_Noise);
			float Erosion_Noise_Y33 = i.uv2_texcoord2.y;
			float4 temp_cast_4 = (Erosion_Noise_Y33).xxxx;
			float4 saferPower19 = abs( ( ( smoothstepResult7 * saturate( ( tex2D( _Noise, panner15 ) - temp_cast_4 ) ) ) * _Alpha_Strength ) );
			float4 temp_cast_5 = (_Alpha_Contrast).xxxx;
			o.Alpha = ( i.vertexColor.a * saturate( pow( saferPower19 , temp_cast_5 ) ) ).r;
		}

		ENDCG
	}
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=18935
0;0;1920;1059;5077.962;2665.622;3.965675;True;True
Node;AmplifyShaderEditor.TextureCoordinatesNode;14;-1740.39,441.4716;Inherit;False;0;13;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;16;-1706.39,640.4716;Inherit;False;Property;_Noise_Speed;Noise_Speed;2;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TexCoordVertexDataNode;31;-1769.54,-239.1623;Inherit;False;1;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PannerNode;15;-1452.39,444.4716;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;33;-1554.103,-154.3195;Inherit;False;Erosion_Noise_Y;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;13;-1178.39,415.4716;Inherit;True;Property;_Noise;Noise;1;0;Create;True;0;0;0;False;0;False;-1;9423f25a67059a0469f8e82b277d6bd2;9423f25a67059a0469f8e82b277d6bd2;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;6;-1163,-19.5;Inherit;False;0;5;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;32;-1551.941,-229.1623;Inherit;False;Erosion_X;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;34;-1096.353,328.0245;Inherit;False;33;Erosion_Noise_Y;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;9;-686,237.5;Inherit;False;Constant;_Float1;Float 1;1;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;35;-1073.753,236.2155;Inherit;False;32;Erosion_X;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;5;-897,-43.5;Inherit;True;Property;_MainTexture;_MainTexture;0;0;Create;True;0;0;0;False;0;False;-1;bfcd6d6b96a44e64da7f82582c837251;bfcd6d6b96a44e64da7f82582c837251;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleSubtractOpNode;12;-674,420.5;Inherit;False;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;11;-469,368.5;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SmoothstepOpNode;7;-500,82.5;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;1,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;10;-322,205.5;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;18;-223.8671,347.8547;Inherit;False;Property;_Alpha_Strength;Alpha_Strength;3;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;17;-17.90561,201.2821;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;20;-10.07043,348.6137;Inherit;False;Property;_Alpha_Contrast;Alpha_Contrast;4;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;24;-276.1387,-474.677;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PowerNode;19;160.9296,202.6137;Inherit;False;True;2;0;COLOR;0,0,0,0;False;1;FLOAT;1;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;28;32.42673,-236.0045;Inherit;False;Property;_Emission_Strength;Emission_Strength;5;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;25;-7.604839,-390.8678;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;21;349.9296,203.6137;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;26;238.3882,-382.5771;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;29;246.2234,-235.2455;Inherit;False;Property;_Emission_Contrast;Emission_Contrast;6;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DepthFade;23;647.9296,301.6137;Inherit;False;True;True;True;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;8;-1044,158.5;Inherit;False;Constant;_Float0;Float 0;1;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;22;536.9296,172.6137;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.PowerNode;27;417.2234,-381.2455;Inherit;False;True;2;0;COLOR;0,0,0,0;False;1;FLOAT;1;False;1;COLOR;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;36;945.0392,-37.68172;Float;False;True;-1;2;ASEMaterialInspector;0;0;Unlit;VAP1//Impact;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Transparent;0.5;True;False;0;True;Transparent;;Transparent;All;18;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;False;2;5;False;-1;10;False;-1;3;1;False;-1;10;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;0;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;15;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;15;0;14;0
WireConnection;15;2;16;0
WireConnection;33;0;31;2
WireConnection;13;1;15;0
WireConnection;32;0;31;1
WireConnection;5;1;6;0
WireConnection;12;0;13;0
WireConnection;12;1;34;0
WireConnection;11;0;12;0
WireConnection;7;0;5;0
WireConnection;7;1;35;0
WireConnection;7;2;9;0
WireConnection;10;0;7;0
WireConnection;10;1;11;0
WireConnection;17;0;10;0
WireConnection;17;1;18;0
WireConnection;19;0;17;0
WireConnection;19;1;20;0
WireConnection;25;0;24;0
WireConnection;25;1;5;0
WireConnection;21;0;19;0
WireConnection;26;0;25;0
WireConnection;26;1;28;0
WireConnection;22;0;24;4
WireConnection;22;1;21;0
WireConnection;27;0;26;0
WireConnection;27;1;29;0
WireConnection;36;2;27;0
WireConnection;36;9;22;0
ASEEND*/
//CHKSM=567D5CB70D6933013CBBA2ED66C74C8BC3D7C8F7