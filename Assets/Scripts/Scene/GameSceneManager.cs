using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool logOperations;


    private readonly Dictionary<string, object> _data = new Dictionary<string, object>();


    public bool IsLoading { get; private set; }


    public event Action<string> OnBeforeSceneLoadByName;
    public event Action<int>    OnBeforeSceneLoadByIndex;
    public event Action<string> OnAfterSceneLoadedByName;
    public event Action<int>    OnAfterSceneLoadedByIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (logOperations) Debug.Log("[GameSceneManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }


    public void LoadScene(string sceneName, SceneTransition transition = null)
    {
        if (transition != null)
        {
            StartCoroutine(LoadWithTransition(() => SceneManager.LoadSceneAsync(sceneName), transition, sceneName: sceneName));
        }
        else
        {
            StartCoroutine(LoadDirect(() => SceneManager.LoadSceneAsync(sceneName), sceneName: sceneName));
        }
    }

    public void LoadScene(int buildIndex, SceneTransition transition = null)
    {
        if (transition != null)
        {
            StartCoroutine(LoadWithTransition(() => SceneManager.LoadSceneAsync(buildIndex), transition, buildIndex: buildIndex));
        }
        else
        {
            StartCoroutine(LoadDirect(() => SceneManager.LoadSceneAsync(buildIndex), buildIndex: buildIndex));
        }
    }

    private IEnumerator LoadDirect(Func<AsyncOperation> load, string sceneName = null, int buildIndex = -1)
    {
        if (sceneName != null) OnBeforeSceneLoadByName?.Invoke(sceneName);
        if (buildIndex >= 0)   OnBeforeSceneLoadByIndex?.Invoke(buildIndex);

        if (logOperations) Debug.Log("[GameSceneManager] Loading scene (direct)...");
        IsLoading = true;
        var op = load.Invoke();
        while (!op.isDone) yield return null;
        IsLoading = false;

        if (sceneName != null) OnAfterSceneLoadedByName?.Invoke(sceneName);
        if (buildIndex >= 0)   OnAfterSceneLoadedByIndex?.Invoke(buildIndex);
    }

    private IEnumerator LoadWithTransition(Func<AsyncOperation> load, SceneTransition transition, string sceneName = null, int buildIndex = -1)
    {
        if (sceneName != null) OnBeforeSceneLoadByName?.Invoke(sceneName);
        if (buildIndex >= 0)   OnBeforeSceneLoadByIndex?.Invoke(buildIndex);

        if (logOperations) Debug.Log("[GameSceneManager] Transition: OUT");
        yield return transition.PlayOut(this);

        if (logOperations) Debug.Log("[GameSceneManager] Loading scene...");
        IsLoading = true;
        var op = load.Invoke();
        while (!op.isDone) yield return null;
        IsLoading = false;

        if (logOperations) Debug.Log("[GameSceneManager] Transition: IN");
        yield return transition.PlayIn(this);

        if (sceneName != null) OnAfterSceneLoadedByName?.Invoke(sceneName);
        if (buildIndex >= 0)   OnAfterSceneLoadedByIndex?.Invoke(buildIndex);
    }

    public IEnumerator WaitUntilLoaded()
    {
        while (IsLoading) yield return null;
    }

    public void QuitGame()
    {
    #if UNITY_EDITOR
        if (logOperations) Debug.Log("[GameSceneManager] QuitGame (Editor stop play)");
        UnityEditor.EditorApplication.isPlaying = false;
    #else
        if (logOperations) Debug.Log("[GameSceneManager] QuitGame (Application.Quit)");
        Application.Quit();
    #endif
    }


    public bool HasPersistentData(string key) => _data.ContainsKey(key);

    public void SetPersistentData<T>(string key, T value)
    {
        _data[key] = value;
        if (logOperations) Debug.Log($"[GameSceneManager] Set '{key}' = {value} ({typeof(T).Name})");
    }

    public T GetPersistentData<T>(string key, T defaultValue = default)
    {
        if (_data.TryGetValue(key, out var obj) && obj is T typed)
        {
            if (logOperations) Debug.Log($"[GameSceneManager] Get '{key}' -> {typed} ({typeof(T).Name})");
            return typed;
        }
        if (logOperations) Debug.Log($"[GameSceneManager] Get '{key}' -> default ({typeof(T).Name})");
        return defaultValue;
    }

    public bool RemovePersistentData(string key)
    {
        var removed = _data.Remove(key);
        if (logOperations && removed) Debug.Log($"[GameSceneManager] Removed '{key}'");
        return removed;
    }

    public void ClearAllPersistentData()
    {
        _data.Clear();
        if (logOperations) Debug.Log("[GameSceneManager] Cleared all data.");
    }
}
