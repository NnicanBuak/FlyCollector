using UnityEngine;

namespace Game.Scripts.Core
{
    public class ResultsSceneController : MonoBehaviour
    {
        private GameResult _result;

        private void Awake()
        {
            var gsm = GameSceneManager.Instance;
            if (gsm != null)
            {
                _result = gsm.GetPersistentData<GameResult>("gameResult");
                if (_result == null)
                {
                    Debug.LogWarning("GameSceneManager.GetPersistentData('gameResult') is null. Возможно, сцена результатов загружена напрямую.");
                }
            }
            else
            {
                Debug.LogWarning("GameSceneManager.Instance is null.");
            }
            // Здесь используйте _result для отображения UI
        }
    }
}
