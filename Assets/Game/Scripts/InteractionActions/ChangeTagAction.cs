using System.Collections;
using UnityEngine;

public class ChangeTagAction : InteractionActionBase
{
    [SerializeField] private string tagName;
    [SerializeField] private GameObject obj;

    public override IEnumerator Execute(InteractionContext ctx)
    {
        GameObject target = obj != null ? obj : ctx.GameObject;
        if (target == null) yield break;

        if (string.IsNullOrEmpty(tagName))
        {
            Debug.LogWarning($"{nameof(ChangeTagAction)}: Имя тега не задано.");
            yield break;
        }

#if UNITY_EDITOR
        bool tagExists = System.Array.Exists(UnityEditorInternal.InternalEditorUtility.tags, t => t == tagName);
        if (!tagExists)
        {
            Debug.LogError($"{nameof(ChangeTagAction)}: Тег \"{tagName}\" не существует.");
            yield break;
        }
#endif
        target.tag = tagName;
        yield break;
    }
}