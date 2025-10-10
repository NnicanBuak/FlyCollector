using UnityEngine;

public class CameraPushSystem
{
    public enum PushMode { OnClick, OnHover }
    public enum PushButton { LeftClick, RightClick }

    private readonly float pushForce;
    private readonly AudioClip pushDefaultSound;
    private readonly float pushSoundVolume;
    private readonly AudioSource audioSource;
    private readonly bool playPushSound;

    private GameObject lastPushedObject;
    private float lastPushTime;
    private float pushCooldown = 0.3f;

    public PushMode Mode { get; set; } = PushMode.OnHover;
    public PushButton Button { get; set; } = PushButton.RightClick;

    public CameraPushSystem(
        float pushForce,
        AudioClip pushDefaultSound,
        float pushSoundVolume,
        AudioSource audioSource,
        bool playPushSound,
        float pushCooldown = 0.3f)
    {
        this.pushForce = pushForce;
        this.pushDefaultSound = pushDefaultSound;
        this.pushSoundVolume = pushSoundVolume;
        this.audioSource = audioSource;
        this.playPushSound = playPushSound;
        this.pushCooldown = pushCooldown;
    }

    public void SetCooldown(float cooldown) => pushCooldown = cooldown;

    public void TryPushOnHover(IPushable pushable, Vector3 hitPoint, Vector3 direction)
    {
        if (Mode != PushMode.OnHover || pushable == null)
            return;

        GameObject pushTarget = ((MonoBehaviour)pushable).gameObject;
        bool canPush = lastPushedObject != pushTarget || (Time.time - lastPushTime) >= pushCooldown;

        if (canPush)
        {
            PushObject(pushTarget, hitPoint, direction);
            lastPushedObject = pushTarget;
            lastPushTime = Time.time;
        }
    }

    public bool TryPushOnClick(IPushable pushable, Vector3 hitPoint, Vector3 direction, bool leftClick, bool rightClick, int focusStackCount)
    {
        if (Mode != PushMode.OnClick || focusStackCount > 0 || pushable == null)
            return false;

        bool isPushButtonPressed = (Button == PushButton.LeftClick && leftClick) ||
                                   (Button == PushButton.RightClick && rightClick);

        if (!isPushButtonPressed)
            return false;

        GameObject pushTarget = ((MonoBehaviour)pushable).gameObject;

        PushObject(pushTarget, hitPoint, direction);
        return true;
    }

    private void PushObject(GameObject target, Vector3 hitPoint, Vector3 direction)
    {
        var pushSession = new PushSession(
            target,
            hitPoint,
            pushForce,
            direction,
            pushDefaultSound,
            pushSoundVolume,
            audioSource,
            playPushSound);

        pushSession.Execute();
    }
}
