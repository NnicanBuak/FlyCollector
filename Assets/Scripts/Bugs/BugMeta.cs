// BugMeta.cs
using UnityEngine;

/// <summary>
/// Метаданные жука на инстансе — связывает объект со "файловым" именем (FLY, FLY.001 и т.п.)
/// </summary>
public class BugMeta : MonoBehaviour
{
    [Tooltip("Имя файла жука (например: FLY, FLY.001, FLY.002)")]
    public string FileName;
}