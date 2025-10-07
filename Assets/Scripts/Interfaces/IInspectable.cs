using UnityEngine;

/// <summary>
/// Интерфейс для объектов, которые можно инспектировать (приблизить к камере)
/// </summary>
public interface IInspectable
{
    void OnHoverEnter();
    void OnHoverExit();
    void OnInspect(Camera playerCamera);
    void OnInspectBegin();
    void OnInspectEnd(); 
    
    /// <summary>
    /// Получить желаемую ротацию для инспекции
    /// </summary>
    Quaternion GetInspectRotation();
    
    /// <summary>
    /// Проверить, использует ли объект кастомную ориентацию
    /// </summary>
    bool UsesCustomOrientation();
}