using UnityEngine;
using UnityEngine.Localization;

namespace Game.Scripts.Localization
{

    public class LocalizedMaterial : MonoBehaviour
    {
        [SerializeField] private LocalizedAsset<Material> localizedMaterial;
        [SerializeField] private bool useSharedMaterial = true;

        private Renderer targetRenderer;

        private void OnEnable()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();

            if (localizedMaterial != null && localizedMaterial.IsEmpty == false)
            {
                localizedMaterial.AssetChanged += OnMaterialChanged;
                localizedMaterial.LoadAssetAsync();
            }
        }

        private void OnDisable()
        {
            if (localizedMaterial != null && localizedMaterial.IsEmpty == false)
            {
                localizedMaterial.AssetChanged -= OnMaterialChanged;
            }
        }

        private void OnMaterialChanged(Material newMaterial)
        {
            if (newMaterial != null && targetRenderer != null)
            {
                if (useSharedMaterial)
                    targetRenderer.sharedMaterial = newMaterial;
                else
                    targetRenderer.material = newMaterial;
            }
        }
    }

}