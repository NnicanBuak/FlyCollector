using System;

namespace Game.Scripts.Localization
{
    public interface ILocalizationService {
        string CurrentLocale { get; }
        event Action<string> LanguageChanged;
        string Get(string key, params object[] args);
        T GetAsset<T>(string assetKey) where T : UnityEngine.Object;
        void SetLocale(string locale);
    }
}