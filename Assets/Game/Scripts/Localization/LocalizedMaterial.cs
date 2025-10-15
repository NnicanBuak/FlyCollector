using Game.Scripts.Localization;
using UnityEngine;
using UnityEngine.UI;
    
namespace Localization
{

    public class LocalizedMaterial : MonoBehaviour {
        public string assetKey; // напр. "tex.menu.play_button"
        public Renderer targetRenderer; // или оставьте null и возьмите GetComponent<Renderer>()
        public bool useSharedMaterial = true;

        void Awake() {
            LocalizationService.Instance.LanguageChanged += _ => Refresh();
        }
        void OnEnable() => Refresh();
        void OnDestroy() {
            if (LocalizationService.Instance != null)
                LocalizationService.Instance.LanguageChanged -= _ => Refresh();
        }

        public void Refresh() {
            var mat = LocalizationService.Instance.GetAsset<Material>(assetKey);
            var r = targetRenderer ?? GetComponent<Renderer>();
            if (r && mat) {
                if (useSharedMaterial) r.sharedMaterial = mat;
                else r.material = mat;
            }
        }
    }

}