// DevSceneHotkeyRuntime.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class DevSceneHotkeyRuntime : MonoBehaviour
{
    [Header("Enable in builds")]
    [Tooltip("Разрешить хоткеи в релизной сборке. По умолчанию только Editor/Development.")]
    public bool enableInRelease = false;

    [Header("Behavior")]
    public bool wrapAround = true;
    public bool logActions = true;

    private static DevSceneHotkeyRuntime _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (_instance != null) return;
        var go = new GameObject("[DevSceneHotkeyRuntime]");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.DontSave;
        _instance = go.AddComponent<DevSceneHotkeyRuntime>();
    }

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool allowed = true;
#else
        bool allowed = enableInRelease;
#endif
        if (!allowed) return;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        // -------- New Input System --------
        HandleNewInputSystem();
#else
        // -------- Old Input Manager (legacy) --------
        HandleLegacyInput();
#endif
    }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    private static bool IsPressed(Keyboard kb, Key key)
    {
        return kb != null && kb[key].isPressed;
    }

    private void HandleNewInputSystem()
    {
        var kb = Keyboard.current;
        var gp = Gamepad.current;

        if (kb != null)
        {
            bool ctrl =
                (kb.leftCtrlKey?.isPressed ?? false) ||
                (kb.rightCtrlKey?.isPressed ?? false);

            // Cmd (mac) / Win (windows) — через индексатор, т.к. metaKey может отсутствовать в пакете
            bool cmdOrWin =
                IsPressed(kb, Key.LeftCommand) || IsPressed(kb, Key.RightCommand) ||
                IsPressed(kb, Key.LeftWindows) || IsPressed(kb, Key.RightWindows);

            bool alt =
                (kb.leftAltKey?.isPressed ?? false) ||
                (kb.rightAltKey?.isPressed ?? false);

            bool ctrlOrCmd = ctrl || cmdOrWin;

            if (ctrlOrCmd && alt && (kb.nKey?.wasPressedThisFrame ?? false)) LoadByOffset(+1);
            if (ctrlOrCmd && alt && (kb.bKey?.wasPressedThisFrame ?? false)) LoadByOffset(-1);
            if (ctrlOrCmd && alt && (kb.rKey?.wasPressedThisFrame ?? false)) LoadByOffset(0);
        }

        if (gp != null)
        {
            bool shoulder = gp.rightShoulder?.isPressed ?? false;
            if (shoulder && gp.dpad.right.wasPressedThisFrame) LoadByOffset(+1);
            if (shoulder && gp.dpad.left.wasPressedThisFrame)  LoadByOffset(-1);
            if (shoulder && gp.buttonSouth.wasPressedThisFrame) LoadByOffset(0);
        }
    }
#endif


    private void LoadByOffset(int delta)
    {
        int total = SceneManager.sceneCountInBuildSettings;
        if (total <= 0)
        {
            if (logActions) Debug.LogWarning("[DevHotkey] В Build Settings нет сцен.");
            return;
        }

        int cur = SceneManager.GetActiveScene().buildIndex;
        int target = delta == 0 ? Mathf.Max(cur, 0) : NextIndex(cur, delta, total, wrapAround);

        if (target < 0 || target >= total)
        {
            if (logActions) Debug.LogWarning($"[DevHotkey] Некорректный индекс сцены: {target}");
            return;
        }

        if (logActions)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(target);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            string action = delta == 0 ? "RELOAD" : (delta > 0 ? "NEXT" : "PREV");
            Debug.Log($"[DevHotkey] {action} → #{target} '{name}'");
        }

        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadScene(target);
        else
            SceneManager.LoadSceneAsync(target);
    }

    private static int NextIndex(int current, int delta, int total, bool wrap)
    {
        if (current < 0) return 0;
        int next = current + delta;
        if (wrap)
        {
            if (next < 0)      next = (next % total + total) % total;
            if (next >= total) next = next % total;
        }
        else
        {
            next = Mathf.Clamp(next, 0, total - 1);
        }
        return next;
    }
}
