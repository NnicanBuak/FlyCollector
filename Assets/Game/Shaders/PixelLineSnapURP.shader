Shader "Unlit/HandwriteLine_PSX"
{
    Properties
    {
        _BaseColor("Color", Color) = (0,0,0,1)
        _MainTex("MainTex", 2D) = "white" {}
        _NoiseScale("Noise Scale", Float) = 50.0
        _WavyAmount("Wavy Amount", Float) = 3.0
        _ThicknessJitter("Thickness Jitter", Float) = 0.4

        // Сколько «экрана» хотим по ВЫСОТЕ, как в PSX-стиле.
        _TargetPixelsY("Target Pixels (Vertical)", Float) = 256.0
    }

    SubShader
    {
        Tags{ "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "Forward"
            Tags{ "LightMode"="UniversalForward" }
            Blend Off
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _MainTex_ST;
                float  _NoiseScale;
                float  _WavyAmount;
                float  _ThicknessJitter;
                float  _TargetPixelsY;
            CBUFFER_END

            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float4 color : COLOR;
                float2 screenPos : TEXCOORD1;
                float linePos : TEXCOORD2; // Позиция вдоль линии
            };

            // Простой hash для псевдослучайных чисел
            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Процедурный шум
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // smoothstep

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Фрактальный шум (несколько октав)
            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                
                for(int i = 0; i < 4; i++)
                {
                    value += amplitude * noise(p * frequency);
                    frequency *= 2.0;
                    amplitude *= 0.5;
                }
                
                return value;
            }

            v2f vert(appdata v)
            {
                v2f o;

                // object -> world -> clip
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float4 posCS = TransformWorldToHClip(float4(posWS, 1.0));

                // Авто-расчёт шага в пикселях экрана под целевую «высоту» (PSX-стиль)
                float targetY = max(_TargetPixelsY, 1.0);
                float stepPx  = max(floor(_ScreenParams.y / targetY + 0.5), 1.0);

                // Снэп к пиксельной сетке (экранные пиксели по stepPx)
                float2 ndc     = posCS.xy / posCS.w;
                float2 screen  = 0.5 * (ndc + 1.0) * _ScreenParams.xy;
                
                // Используем UV.x как позицию вдоль линии для волнистости
                float2 noiseCoord = float2(v.uv.x * _NoiseScale, 0);
                float noiseValue = fbm(noiseCoord);
                
                // Смещаем перпендикулярно линии (по Y в экранных координатах)
                float perpOffset = (noiseValue - 0.5) * _WavyAmount * stepPx;
                
                // Добавляем небольшое смещение по обоим осям для "дрожания"
                screen.x += perpOffset * 0.3;
                screen.y += perpOffset;
                
                // Теперь снэпим к сетке
                screen = round(screen / stepPx) * stepPx;
                float2 ndcSnap = (screen / _ScreenParams.xy) * 2.0 - 1.0;
                posCS.xy = ndcSnap * posCS.w;

                o.pos = posCS;
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                o.screenPos = screen;
                o.linePos = v.uv.x;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // Рукописная «лохматость» через шум: варьируем толщину
                float2 noiseCoord = float2(i.linePos * _NoiseScale, i.uv.y * _NoiseScale * 0.5);
                float n = fbm(noiseCoord);
                
                // Варьируем видимость по UV.y (поперёк линии) с помощью шума
                float edgeFade = abs(i.uv.y - 0.5) * 2.0; // 0 в центре, 1 на краях
                float jitter = (n - 0.5) * _ThicknessJitter;
                float threshold = 1.0 - edgeFade + jitter;
                
                // Дискретный clipping для чёткого края (пиксельный стиль)
                clip(threshold - 0.4);

                half4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _BaseColor * i.color;
                return half4(baseCol.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
