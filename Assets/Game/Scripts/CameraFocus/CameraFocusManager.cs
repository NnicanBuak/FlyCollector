using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class /**/CameraFocusManager
{
    private readonly Camera cam;
    private readonly float focusFlyTime;
    private readonly float focusRotationSpeed;
    private readonly AudioSource audioSource;
    private readonly AudioClip unavailableSound;
    private readonly bool warnUnavailableFocus;
    private readonly bool showDebug;

    private readonly Stack<FocusSession> focusStack = new Stack<FocusSession>();
    private bool pendingReturnPositionUpdate;

    public bool IsAtZeroFocus => focusStack.Count == 0;
    public int FocusDepth => focusStack.Count;

    public CameraFocusManager(
        Camera camera,
        float focusFlyTime,
        float focusRotationSpeed,
        AudioSource audioSource = null,
        AudioClip unavailableSound = null,
        bool warnUnavailableFocus = true,
        bool showDebug = false)
    {
        this.cam = camera;
        this.focusFlyTime = focusFlyTime;
        this.focusRotationSpeed = focusRotationSpeed;
        this.audioSource = audioSource;
        this.unavailableSound = unavailableSound;
        this.warnUnavailableFocus = warnUnavailableFocus;
        this.showDebug = showDebug;
    }

    public void Update()
    {
        if (focusStack.Count == 0)
            return;

        var top = focusStack.Peek();
        top.Update();

        if (top.IsFinished())
        {
            focusStack.Pop();


            if (FocusLevelManager.Instance != null)
            {
                FocusLevelManager.Instance.GoToPreviousLevel();
                if (showDebug)
                    Debug.Log($"[CameraFocusManager] Restored nest level to: {FocusLevelManager.Instance.CurrentNestLevel}");
            }

            if (showDebug)
                Debug.Log($"[CameraFocusManager] Focus finished. Remaining: {focusStack.Count}");

            if (focusStack.Count == 0)
            {
                pendingReturnPositionUpdate = false;

                // Track camera mode change when exiting all focus
                GAManager.Instance.TrackCameraModeChange("Focus", "Normal");
            }
        }
    }

    public bool IsAnimating()
    {
        return focusStack.Count > 0 && focusStack.Peek().IsAnimating;
    }

    public GameObject GetCurrentFocusedObject()
    {
        return focusStack.Count > 0 ? focusStack.Peek().GetTarget() : null;
    }

    public bool TryRequestExit(bool escPressed, bool rmbPressed)
    {
        if (focusStack.Count == 0)
            return false;

        var top = focusStack.Peek();
        if (!top.IsAnimating && (escPressed || rmbPressed))
        {
            top.RequestExit();
            return true;
        }

        return false;
    }

    public bool TryStartFocus(GameObject target)
    {
        var focusable = target.GetComponent<IFocusable>();
        if (focusable == null)
        {
            Debug.LogWarning($"[CameraFocusManager] IFocusable not found on {target.name}");
            return false;
        }

        int currentNestLevel = FocusLevelManager.Instance != null
            ? FocusLevelManager.Instance.CurrentNestLevel
            : 0;

        if (!focusable.IsAvailableAtNestLevel(currentNestLevel))
        {
            if (showDebug)
                Debug.Log($"[CameraFocusManager] Object {target.name} unavailable at level {currentNestLevel}");

            if (warnUnavailableFocus)
            {
                if (audioSource != null && unavailableSound != null)
                    audioSource.PlayOneShot(unavailableSound);

                Debug.LogWarning($"Object available only at nest level {focusable.GetRequiredNestLevel()}");
            }
            return false;
        }

        if (showDebug)
        {
            Debug.Log($"[CameraFocusManager] === FOCUS START ===");
            Debug.Log($"[CameraFocusManager] Focus on: {target.name}");
            Debug.Log($"[CameraFocusManager] Current level: {currentNestLevel} → target: {focusable.GetTargetNestLevel()}");
            Debug.Log($"[CameraFocusManager] Focus stack BEFORE: {focusStack.Count}");
        }

        var newFocus = new FocusSession(cam, target.transform, focusFlyTime, focusRotationSpeed, null);
        focusStack.Push(newFocus);
        newFocus.Begin();


        if (FocusLevelManager.Instance != null)
        {
            FocusLevelManager.Instance.SetNestLevel(focusable.GetTargetNestLevel(), target);
            if (showDebug)
                Debug.Log($"[CameraFocusManager] Set nest level to: {focusable.GetTargetNestLevel()}");
        }

        // Track camera mode change when entering focus
        if (focusStack.Count == 1)
        {
            GAManager.Instance.TrackCameraModeChange("Normal", "Focus");
        }

        if (showDebug)
            Debug.Log($"[CameraFocusManager] Focus stack AFTER: {focusStack.Count}");

        pendingReturnPositionUpdate = true;
        return true;
    }

    public IEnumerator ExitAllFocusCoroutine(System.Action<Vector3, Quaternion> updateReturnPosition, bool skipReturnAnim = false)
    {
        while (focusStack.Count > 0)
        {
            var top = focusStack.Peek();

            if (skipReturnAnim)
            {
                // мгновенно завершить вершину стека, не дожидаясь апдейтов
                top.ForceFinishWithoutReturn();                           // помечает finished и вызывает onFinish :contentReference[oaicite:0]{index=0}
                focusStack.Pop();

                // откатываем уровень фокуса (как в обычной ветке) 
                FocusLevelManager.Instance?.GoToPreviousLevel();           // логика сброса уровня у вас уже есть в корутине :contentReference[oaicite:1]{index=1}
                // продолжаем цикл без yield
                continue;
            }
            else
            {
                if (!top.IsAnimating)                                      // обычный путь: просим выйти и ждём кадр
                    top.RequestExit();                                     // :contentReference[oaicite:2]{index=2}
            }

            // только для анимированного выхода
            yield return null;

            if (top.IsFinished() && focusStack.Count > 0)
            {
                focusStack.Pop();

                if (FocusLevelManager.Instance != null)
                {
                    FocusLevelManager.Instance.GoToPreviousLevel();        // :contentReference[oaicite:3]{index=3}
                }
            }
        }

        pendingReturnPositionUpdate = false;                               // :contentReference[oaicite:4]{index=4}
        if (cam != null && updateReturnPosition != null)
            updateReturnPosition(cam.transform.position, cam.transform.rotation); // :contentReference[oaicite:5]{index=5}
    }
    
    public FocusSession PeekTop()
    {
        return focusStack.Count > 0 ? focusStack.Peek() : null;
    }

    public void ForceFinishTopWithoutReturn()
    {
        var top = PeekTop();
        top?.ForceFinishWithoutReturn();
    }


    public bool ShouldUpdateReturnPosition()
    {
        return pendingReturnPositionUpdate && focusStack.Count == 0;
    }

    public void ClearPendingReturnUpdate()
    {
        pendingReturnPositionUpdate = false;
    }
}
