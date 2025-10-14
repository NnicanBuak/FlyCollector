using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMouseFollow
{
    private readonly Camera cam;
    private readonly float mouseFollowAmount;
    private readonly float mouseFollowSpeed;
    private Quaternion baseRotation;
    private bool baseRotationInitialized;

    // NEW: пауза (реентерабельная)
    private int pauseCount = 0;
    public void Pause()  { pauseCount++; }
    public void Resume() { if (pauseCount > 0) pauseCount--; }
    public bool IsPaused => pauseCount > 0;

    public CameraMouseFollow(Camera camera, float mouseFollowAmount, float mouseFollowSpeed)
    {
        this.cam = camera;
        this.mouseFollowAmount = mouseFollowAmount;
        this.mouseFollowSpeed = mouseFollowSpeed;

        if (cam != null)
        {
            baseRotation = cam.transform.rotation;
            baseRotationInitialized = true;
        }
    }

    public void UpdateBaseRotation()
    {
        if (cam != null)
        {
            baseRotation = cam.transform.rotation;
            baseRotationInitialized = true;
        }
    }

    // NEW: явный ребейз на заданный поворот (удобно после "домой")
    public void RebaseTo(Quaternion rotation)
    {
        baseRotation = rotation;
        baseRotationInitialized = true;
    }

    public void Update(Mouse mouse)
    {
        // NEW: если пауза — не трогаем камеру
        if (IsPaused) return;

        if (!baseRotationInitialized || mouse == null || cam == null)
            return;

        Vector2 mousePos = mouse.position.ReadValue();
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 mouseOffset = mousePos - screenCenter;

        float normalizedX = Mathf.Clamp(mouseOffset.x / (Screen.width * 0.5f), -1f, 1f);
        float normalizedY = Mathf.Clamp(mouseOffset.y / (Screen.height * 0.5f), -1f, 1f);

        float yawOffset = normalizedX * mouseFollowAmount;
        float pitchOffset = -normalizedY * mouseFollowAmount;

        Quaternion targetRotation = baseRotation * Quaternion.Euler(pitchOffset, yawOffset, 0f);

        cam.transform.rotation = Quaternion.Lerp(
            cam.transform.rotation,
            targetRotation,
            mouseFollowSpeed * Time.deltaTime
        );
    }
} 