using System.Collections;
using UnityEngine;

[AddComponentMenu("Interactions/Actions/Play Audio")]
public class PlayAudioAction : InteractionActionBase
{
    [Header("Аудио")]
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    [Header("Источник")]
    [Tooltip("Если задан, играет через этот AudioSource. Иначе — PlayClipAtPoint у позиции контекста/объекта.")]
    [SerializeField] private AudioSource output;

    [Header("Поведение")]
    [Tooltip("Ждать ли окончания клипа перед завершением Execute()")]
    [SerializeField] private bool waitUntilFinished = false;

    [Tooltip("Случайный питч в диапазоне")]
    [SerializeField] private bool randomizePitch = false;

    [SerializeField] private Vector2 pitchRange = new Vector2(1f, 1f);

    public override IEnumerator Execute(InteractionContext ctx)
    {
        if (!clip)
        {
            Debug.LogWarning("[PlayAudioAction] Не задан AudioClip.", this);
            yield break;
        }

        float appliedPitch = 1f;
        if (output)
        {
            appliedPitch = randomizePitch ? Random.Range(pitchRange.x, pitchRange.y) : 1f;
            float oldPitch = output.pitch;

            if (randomizePitch) output.pitch = appliedPitch;

            // Если у источника уже стоит другой клип — безопасней одноразово
            output.PlayOneShot(clip, volume);

            if (waitUntilFinished)
                yield return new WaitForSeconds(clip.length / Mathf.Max(appliedPitch, 0.0001f));

            if (randomizePitch) output.pitch = oldPitch;
        }
        else
        {
            // Без источника — звучит в мире возле объекта взаимодействия
            Vector3 pos = ctx.Transform ? ctx.Transform.position : transform.position;
            AudioSource.PlayClipAtPoint(clip, pos, volume);

            if (waitUntilFinished)
                yield return new WaitForSeconds(clip.length);
        }
    }
}