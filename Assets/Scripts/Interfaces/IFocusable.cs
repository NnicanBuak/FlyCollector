using UnityEngine;

/// <summary>
/// Интерфейс для объектов, на которые можно сфокусировать камеру
/// </summary>
public interface IFocusable
{
    void OnFocusHoverEnter();
    void OnFocusHoverExit();
    void OnFocusStart();
    void OnFocusEnd();
    
    /// <summary>
    /// Получить позицию для камеры при фокусе
    /// </summary>
    Vector3 GetCameraPosition();
    
    /// <summary>
    /// Получить поворот для камеры при фокусе
    /// </summary>
    Quaternion GetCameraRotation();
    
    /// <summary>
    /// Проверить, заблокирована ли позиция камеры (отключает вращение мышью)
    /// </summary>
    bool IsCameraPositionLocked();
    
    /// <summary>
    /// Получить центр объекта для фокуса (куда направлять камеру)
    /// </summary>
    Vector3 GetFocusCenter();
    
    // === НОВЫЕ МЕТОДЫ ДЛЯ ВЛОЖЕННОСТИ ===
    
    /// <summary>
    /// Получить требуемый уровень вложенности для взаимодействия с объектом
    /// </summary>
    int GetRequiredNestLevel();
    
    /// <summary>
    /// Получить целевой уровень вложенности при фокусе на объект
    /// </summary>
    int GetTargetNestLevel();
    
    /// <summary>
    /// Проверить, доступен ли объект на текущем уровне вложенности
    /// </summary>
    bool IsAvailableAtNestLevel(int currentLevel);
}