using UnityEngine;

public interface IPushable
{
    void OnPushHoverEnter();
    void OnPushHoverExit();
    void OnPushed(Vector3 force, Vector3 hitPoint);
    float GetPushForceMultiplier();
}

public class PushableObject : MonoBehaviour, IPushable
{
    [Header("Push Settings")]
    [Tooltip("Множитель силы толкания для этого объекта")]
    [SerializeField] private float pushForceMultiplier = 1f;
    
    [Tooltip("Звук при толкании (если не задан, используется общий из CameraController)")]
    [SerializeField] private AudioClip customPushSound;
    
    [Tooltip("Громкость кастомного звука")]
    [SerializeField] private float customSoundVolume = 0.5f;

    [Header("Visual Feedback")]
    [Tooltip("Использовать outline при наведении")]
    [SerializeField] private bool useOutline = true;
    
    [SerializeField] private Color pushHoverColor = new Color(255, 100, 0);
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private Outline outline;
    private AudioSource audioSource;

    void Awake()
    {
        outline = GetComponent<Outline>();
        
        if (outline != null && useOutline)
        {
            outline.enabled = false;
        }

        if (customPushSound != null)
        {
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }
    }

    public void OnPushHoverEnter()
    {
        if (useOutline && outline != null)
        {
            outline.enabled = true;
            outline.OutlineColor = pushHoverColor;
        }

        if (showDebugInfo)
        {
            Debug.Log($"[PushableObject] Hover enter: {gameObject.name}");
        }
    }

    public void OnPushHoverExit()
    {
        if (useOutline && outline != null)
        {
            outline.enabled = false;
        }

        if (showDebugInfo)
        {
            Debug.Log($"[PushableObject] Hover exit: {gameObject.name}");
        }
    }

    public void OnPushed(Vector3 force, Vector3 hitPoint)
    {

        var swing = GetComponent<JarSwing>();
        
        if (swing != null && swing.enabled)
        {

            swing.Push(force.normalized, force.magnitude * pushForceMultiplier);
            
            if (showDebugInfo)
            {
                Debug.Log($"[PushableObject] Толкнули качели {gameObject.name} с силой {force.magnitude * pushForceMultiplier}");
            }
        }
        else
        {

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddForceAtPosition(force * pushForceMultiplier, hitPoint, ForceMode.Impulse);
                
                if (showDebugInfo)
                {
                    Debug.Log($"[PushableObject] Откинули {gameObject.name} с силой {force.magnitude * pushForceMultiplier}");
                }
            }
        }


        if (customPushSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(customPushSound, customSoundVolume);
        }
    }

    public float GetPushForceMultiplier()
    {
        return pushForceMultiplier;
    }

    public void SetPushForceMultiplier(float multiplier)
    {
        pushForceMultiplier = multiplier;
    }
}

public class PushSession
{
    private GameObject target;
    private IPushable pushable;
    private Vector3 pushForce;
    private Vector3 hitPoint;
    private AudioClip defaultSound;
    private float defaultVolume;
    private AudioSource audioSource;
    private bool showDebug;

    public PushSession(GameObject target, Vector3 direction, float force, Vector3 hitPoint, 
        AudioClip defaultSound, float defaultVolume, AudioSource audioSource, bool showDebug = false)
    {
        this.target = target;
        this.pushable = target.GetComponent<IPushable>();
        this.pushForce = direction.normalized * force;
        this.hitPoint = hitPoint;
        this.defaultSound = defaultSound;
        this.defaultVolume = defaultVolume;
        this.audioSource = audioSource;
        this.showDebug = showDebug;
    }

    public void Execute()
    {
        if (target == null) return;

        if (pushable != null)
        {

            pushable.OnPushed(pushForce, hitPoint);
        }
        else
        {

            var swing = target.GetComponent<JarSwing>();
            if (swing != null && swing.enabled)
            {
                swing.Push(pushForce.normalized, pushForce.magnitude);
                
                if (showDebug)
                {
                    Debug.Log($"[PushSession] Толкнули качели {target.name} (без PushableObject)");
                }
            }
            else
            {

                var rb = target.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = target.GetComponentInParent<Rigidbody>();
                }

                if (rb != null)
                {
                    rb.AddForceAtPosition(pushForce, hitPoint, ForceMode.Impulse);
                    
                    if (showDebug)
                    {
                        Debug.Log($"[PushSession] Откинули {target.name}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[PushSession] У объекта {target.name} нет Rigidbody или JarSwing!");
                }
            }
        }


        var pushableObj = target.GetComponent<PushableObject>();
        bool hasCustomSound = pushableObj != null && pushableObj.GetComponent<AudioSource>() != null;
        
        if (!hasCustomSound && defaultSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(defaultSound, defaultVolume);
        }
    }
}