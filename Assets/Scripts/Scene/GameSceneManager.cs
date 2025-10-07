// ==== ОСНОВНОЙ МЕНЕДЖЕР СЦЕН ====
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System;

public class GameSceneManager : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private float minLoadingTime = 1f;
    [SerializeField] private bool showDebugInfo = true;
    
    // Singleton
    private static GameSceneManager _instance;
    public static GameSceneManager Instance 
    { 
        get 
        { 
            if (_instance == null)
                _instance = FindObjectOfType<GameSceneManager>();
            return _instance; 
        } 
    }

    // События
    public static event Action<string> OnSceneLoadStarted;
    public static event Action<string, float> OnSceneLoadProgress;
    public static event Action<string> OnSceneLoadCompleted;
    public static event Action<string> OnSceneUnloadStarted;
    public static event Action<string> OnSceneUnloadCompleted;
    
    // Текущее состояние
    public string CurrentScene { get; private set; }
    public string PreviousScene { get; private set; }
    public bool IsLoading { get; private set; }
    
    // Кэш сцен и данных
    private Dictionary<string, SceneData> sceneRegistry = new Dictionary<string, SceneData>();
    private Stack<string> sceneHistory = new Stack<string>();
    private Dictionary<string, object> persistentData = new Dictionary<string, object>();
    
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            InitializeSceneRegistry();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        CurrentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
    }

    void InitializeSceneRegistry()
    {
        // Регистрация всех сцен игры
        RegisterScene("MainMenu", SceneType.Menu, LoadingType.Standard);
        RegisterScene("Game", SceneType.Gameplay, LoadingType.WithProgress);
        RegisterScene("GameOver", SceneType.Menu, LoadingType.Standard);
        RegisterScene("Credits", SceneType.Menu, LoadingType.Standard);
    }

    public void RegisterScene(string sceneName, SceneType type, LoadingType loadingType)
    {
        if (!sceneRegistry.ContainsKey(sceneName))
        {
            sceneRegistry[sceneName] = new SceneData(sceneName, type, loadingType);
        }
    }

    // ==== ОСНОВНЫЕ МЕТОДЫ ЗАГРУЗКИ ====
    
    public void LoadScene(string sceneName, SceneTransition transition = null)
    {
        if (IsLoading)
        {
            Debug.LogWarning("Сцена уже загружается!");
            return;
        }

        if (!sceneRegistry.ContainsKey(sceneName))
        {
            Debug.LogError($"Сцена {sceneName} не зарегистрирована!");
            return;
        }

        StartCoroutine(LoadSceneCoroutine(sceneName, transition));
    }

    public void LoadSceneAdditive(string sceneName, SceneTransition transition = null)
    {
        if (!sceneRegistry.ContainsKey(sceneName))
        {
            Debug.LogError($"Сцена {sceneName} не зарегистрирована!");
            return;
        }

        StartCoroutine(LoadSceneAdditiveCoroutine(sceneName, transition));
    }

    public void UnloadScene(string sceneName)
    {
        if (IsLoading) return;
        
        StartCoroutine(UnloadSceneCoroutine(sceneName));
    }

    public void LoadPreviousScene()
    {
        if (sceneHistory.Count > 0)
        {
            string previousScene = sceneHistory.Pop();
            LoadScene(previousScene);
        }
        else
        {
            Debug.LogWarning("Нет предыдущих сцен в истории!");
        }
    }

    // ==== КОРУТИНЫ ЗАГРУЗКИ ====
    
    IEnumerator LoadSceneCoroutine(string sceneName, SceneTransition transition)
    {
        IsLoading = true;
        float startTime = Time.realtimeSinceStartup;
        
        OnSceneLoadStarted?.Invoke(sceneName);
        
        // Переход (fade out)
        if (transition != null)
        {
            yield return StartCoroutine(transition.FadeOut());
        }

        // Сохраняем историю
        if (!string.IsNullOrEmpty(CurrentScene))
        {
            sceneHistory.Push(CurrentScene);
            PreviousScene = CurrentScene;
        }

        // Загружаем сцену асинхронно
        AsyncOperation asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        while (!asyncLoad.isDone)
        {
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            OnSceneLoadProgress?.Invoke(sceneName, progress);

            // Активируем сцену когда загрузка завершена и прошло минимальное время
            if (asyncLoad.progress >= 0.9f && 
                Time.realtimeSinceStartup - startTime >= minLoadingTime)
            {
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }

        CurrentScene = sceneName;

        // Инициализация сцены
        yield return StartCoroutine(InitializeScene(sceneName));

        // Переход (fade in)
        if (transition != null)
        {
            yield return StartCoroutine(transition.FadeIn());
        }

        IsLoading = false;
        OnSceneLoadCompleted?.Invoke(sceneName);

        if (showDebugInfo)
            Debug.Log($"Сцена {sceneName} загружена за {Time.realtimeSinceStartup - startTime:F2} сек");
    }

    IEnumerator LoadSceneAdditiveCoroutine(string sceneName, SceneTransition transition)
    {
        IsLoading = true;
        OnSceneLoadStarted?.Invoke(sceneName);

        AsyncOperation asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        
        while (!asyncLoad.isDone)
        {
            OnSceneLoadProgress?.Invoke(sceneName, asyncLoad.progress);
            yield return null;
        }

        yield return StartCoroutine(InitializeScene(sceneName));

        IsLoading = false;
        OnSceneLoadCompleted?.Invoke(sceneName);
    }

    IEnumerator UnloadSceneCoroutine(string sceneName)
    {
        OnSceneUnloadStarted?.Invoke(sceneName);

        AsyncOperation asyncUnload = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(sceneName);
        
        while (!asyncUnload.isDone)
        {
            yield return null;
        }

        OnSceneUnloadCompleted?.Invoke(sceneName);
    }

    IEnumerator InitializeScene(string sceneName)
    {
        // Ищем инициализатор сцены
        SceneInitializer initializer = FindObjectOfType<SceneInitializer>();
        if (initializer != null)
        {
            yield return StartCoroutine(initializer.Initialize());
        }
        
        yield return null;
    }

    // ==== УПРАВЛЕНИЕ ДАННЫМИ ====
    
    public void SetPersistentData<T>(string key, T data)
    {
        persistentData[key] = data;
    }

    public T GetPersistentData<T>(string key, T defaultValue = default(T))
    {
        if (persistentData.ContainsKey(key) && persistentData[key] is T)
        {
            return (T)persistentData[key];
        }
        return defaultValue;
    }

    public bool HasPersistentData(string key)
    {
        return persistentData.ContainsKey(key);
    }

    public void ClearPersistentData()
    {
        persistentData.Clear();
    }

    // ==== БЫСТРЫЕ МЕТОДЫ ====
    
    public void LoadMainMenu() => LoadScene("MainMenu");
    public void LoadGameLevel(int levelNumber) => LoadScene($"GameLevel{levelNumber}");
    public void ShowSettings() => LoadSceneAdditive("Settings");
    public void HideSettings() => UnloadScene("Settings");
    public void RestartCurrentScene() => LoadScene(CurrentScene);
    public void QuitGame() => Application.Quit();
}

// ==== ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ ====

[Serializable]
public class SceneData
{
    public string sceneName;
    public SceneType type;
    public LoadingType loadingType;
    public bool isLoaded;

    public SceneData(string name, SceneType sceneType, LoadingType loading)
    {
        sceneName = name;
        type = sceneType;
        loadingType = loading;
        isLoaded = false;
    }
}

public enum SceneType
{
    Menu,
    Gameplay,
    UI,
    Cutscene
}

public enum LoadingType
{
    Standard,
    WithProgress,
    Additive,
    Background
}

// ==== ИНИЦИАЛИЗАТОР СЦЕНЫ (БАЗОВЫЙ КЛАСС) ====

public abstract class SceneInitializer : MonoBehaviour
{
    [Header("Настройки инициализации")]
    [SerializeField] protected float initializationDelay = 0.1f;
    
    public abstract IEnumerator Initialize();
    
    protected virtual void OnSceneLoaded()
    {
        Debug.Log($"Сцена {gameObject.scene.name} инициализирована");
    }
}

// ==== ПРИМЕР КОНКРЕТНОГО ИНИЦИАЛИЗАТОРА ====

public class GameplaySceneInitializer : SceneInitializer
{
    [Header("Gameplay настройки")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform spawnPoint;
    
    public override IEnumerator Initialize()
    {
        yield return new WaitForSeconds(initializationDelay);
        
        // Инициализируем игровые системы
        InitializePlayer();
        InitializeUI();
        InitializeAudio();
        
        OnSceneLoaded();
    }
    
    void InitializePlayer()
    {
        if (playerPrefab != null && spawnPoint != null)
        {
            GameObject player = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
            Debug.Log("Игрок создан");
        }
    }
    
    void InitializeUI()
    {
        // Инициализация UI элементов
        Debug.Log("UI инициализирован");
    }
    
    void InitializeAudio()
    {
        // Настройка аудио для сцены
        Debug.Log("Аудио настроено");
    }
}

// ==== ПЕРЕХОДЫ МЕЖДУ СЦЕНАМИ ====

[System.Serializable]
public class SceneTransition
{
    [SerializeField] private float fadeTime = 1f;
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private CanvasGroup fadeCanvas;
    
    public SceneTransition(float duration = 1f)
    {
        fadeTime = duration;
        InitializeFadeCanvas();
    }

    void InitializeFadeCanvas()
    {
        try
        {
            // Создаем canvas для fade эффекта если его нет
            GameObject fadeObject = GameObject.Find("SceneFadeCanvas");
            if (fadeObject == null)
            {
                fadeObject = new GameObject("SceneFadeCanvas");
                
                // Убеждаемся что объект находится в корне иерархии
                if (fadeObject.transform.parent != null)
                {
                    fadeObject.transform.SetParent(null);
                }
                
                // Настраиваем Canvas
                Canvas canvas = fadeObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 999;
                
                // Добавляем CanvasScaler для адаптивности
                CanvasScaler scaler = fadeObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                
                // Добавляем GraphicRaycaster
                fadeObject.AddComponent<GraphicRaycaster>();
                
                fadeCanvas = fadeObject.AddComponent<CanvasGroup>();
                fadeCanvas.alpha = 0f;
                fadeCanvas.blocksRaycasts = false;
                fadeCanvas.interactable = false;
                
                // Создаем фоновое изображение
                GameObject background = new GameObject("FadeBackground");
                background.transform.SetParent(fadeObject.transform, false);
                
                var image = background.AddComponent<Image>();
                image.color = Color.black;
                image.raycastTarget = false;
                
                // Настраиваем RectTransform для полного покрытия экрана
                var rectTransform = background.GetComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.localPosition = Vector3.zero;
                rectTransform.localScale = Vector3.one;
                
                // ИСПРАВЛЕНО: используем полный путь для избежания Ambiguous reference
                if (Application.isPlaying)
                {
                    UnityEngine.Object.DontDestroyOnLoad(fadeObject);
                    Debug.Log("Fade Canvas создан и установлен как DontDestroyOnLoad");
                }
            }
            else
            {
                fadeCanvas = fadeObject.GetComponent<CanvasGroup>();
                if (fadeCanvas == null)
                {
                    Debug.LogWarning("SceneFadeCanvas найден, но CanvasGroup отсутствует!");
                    fadeCanvas = fadeObject.AddComponent<CanvasGroup>();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка при инициализации Fade Canvas: {e.Message}");
            
            // Fallback - создаем простейший вариант
            CreateSimpleFadeCanvas();
        }
    }

    // Запасной простой вариант
    void CreateSimpleFadeCanvas()
    {
        GameObject fadeObject = new GameObject("SceneFadeCanvas");
        Canvas canvas = fadeObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        
        fadeCanvas = fadeObject.AddComponent<CanvasGroup>();
        fadeCanvas.alpha = 0f;
        fadeCanvas.blocksRaycasts = false;
        
        GameObject background = new GameObject("Background");
        background.transform.SetParent(fadeObject.transform);
        Image image = background.AddComponent<Image>();
        image.color = Color.black;
        
        RectTransform rect = background.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        
        if (Application.isPlaying)
        {
            UnityEngine.Object.DontDestroyOnLoad(fadeObject);
        }
    }

    public IEnumerator FadeOut()
    {
        if (fadeCanvas == null) InitializeFadeCanvas();
        
        if (fadeCanvas != null)
        {
            fadeCanvas.blocksRaycasts = true;
            float elapsedTime = 0f;
            
            while (elapsedTime < fadeTime)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / fadeTime;
                fadeCanvas.alpha = fadeCurve.Evaluate(progress);
                yield return null;
            }
            
            fadeCanvas.alpha = 1f;
        }
    }

    public IEnumerator FadeIn()
    {
        if (fadeCanvas == null) InitializeFadeCanvas();
        
        if (fadeCanvas != null)
        {
            float elapsedTime = 0f;
            
            while (elapsedTime < fadeTime)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / fadeTime;
                fadeCanvas.alpha = 1f - fadeCurve.Evaluate(progress);
                yield return null;
            }
            
            fadeCanvas.alpha = 0f;
            fadeCanvas.blocksRaycasts = false;
        }
    }
}