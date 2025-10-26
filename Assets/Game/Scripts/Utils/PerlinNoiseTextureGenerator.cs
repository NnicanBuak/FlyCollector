using UnityEngine;

namespace Game.Scripts.Utils
{
    public class PerlinNoiseTextureGenerator : MonoBehaviour
    {
        [Header("Настройки текстуры")]
        [SerializeField] private int _textureSize = 256;
        [SerializeField] private float _scale = 20f;
        [SerializeField] private int _octaves = 4;
        [SerializeField] private float _persistence = 0.5f;
        [SerializeField] private float _lacunarity = 2f;
        [SerializeField] private Vector2 _offset = Vector2.zero;

        [Header("Выход")]
        [SerializeField] private string _textureName = "PerlinNoise";
        
        private Texture2D _generatedTexture;

        public Texture2D GeneratedTexture => _generatedTexture;

        private void Start()
        {
            GenerateTexture();
        }

        [ContextMenu("Generate Texture")]
        public void GenerateTexture()
        {
            _generatedTexture = new Texture2D(_textureSize, _textureSize, TextureFormat.RGB24, false);
            _generatedTexture.filterMode = FilterMode.Bilinear;
            _generatedTexture.wrapMode = TextureWrapMode.Repeat;
            _generatedTexture.name = _textureName;

            Color[] pixels = new Color[_textureSize * _textureSize];

            for (int y = 0; y < _textureSize; y++)
            {
                for (int x = 0; x < _textureSize; x++)
                {
                    float noiseValue = GeneratePerlinNoise(x, y);
                    pixels[y * _textureSize + x] = new Color(noiseValue, noiseValue, noiseValue, 1f);
                }
            }

            _generatedTexture.SetPixels(pixels);
            _generatedTexture.Apply();
        }

        private float GeneratePerlinNoise(int x, int y)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float noiseValue = 0f;
            float maxValue = 0f;

            for (int i = 0; i < _octaves; i++)
            {
                float sampleX = (x / (float)_textureSize * _scale + _offset.x) * frequency;
                float sampleY = (y / (float)_textureSize * _scale + _offset.y) * frequency;

                float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);
                noiseValue += perlinValue * amplitude;
                maxValue += amplitude;

                amplitude *= _persistence;
                frequency *= _lacunarity;
            }

            return noiseValue / maxValue;
        }

#if UNITY_EDITOR
        [ContextMenu("Save Texture as Asset")]
        private void SaveTextureAsAsset()
        {
            if (_generatedTexture == null)
            {
                GenerateTexture();
            }

            string path = $"Assets/Resources/Game/Textures/{_textureName}.png";
            
            // Создаём папку если не существует
            string directory = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            byte[] bytes = _generatedTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, bytes);
            
            UnityEditor.AssetDatabase.Refresh();
            Debug.Log($"Texture saved to {path}");
        }
#endif
    }
}

