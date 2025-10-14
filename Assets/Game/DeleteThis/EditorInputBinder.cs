using System;
using UnityEngine;
using UnityEngine.InputSystem; // Для нового Input System

#if UNITY_EDITOR
using UnityEditor; // Если нужно редактор-специфическое
#endif

public class EditorInputBinder : MonoBehaviour
{
    // Ссылка на ваш существующий Input Action Asset (перетащите в Inspector)
    [SerializeField] private InputActionAsset inputActionsAsset;

    public event Action<GameOutcome> Ended;

    // Ссылки на Actions (инициализируем в Awake)
    private InputAction actionQ;
    private InputAction actionA;
    private InputAction actionZ;
    // Добавьте больше, если нужно

    void Awake()
    {
#if UNITY_EDITOR
        // Находим Action Map и Actions по именам (замените на ваши имена)
        var editorMap = inputActionsAsset.FindActionMap("SkipDebug"); // Имя вашего Action Map
        actionQ = editorMap.FindAction("Victory"); // Имя вашей Action
        actionA = editorMap.FindAction("MissMatch");
        actionZ = editorMap.FindAction("Fail");
        // Добавьте: actionC = editorMap.FindAction("ActionC"); и т.д.
#endif
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        // Включаем Actions и привязываем к функциям
        actionQ.Enable();
        actionA.Enable();
        actionZ.Enable();

        // Привязка: performed — срабатывает при выполнении действия (нажатии)
        actionQ.performed += OnActionQ;
        actionA.performed += OnActionA;
        actionZ.performed += OnActionZ;
        // Добавьте: actionC.performed += OnActionC;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        // Отвязываем и выключаем
        actionQ.performed -= OnActionQ;
        actionA.performed -= OnActionA;
        actionZ.performed -= OnActionZ;

        actionQ.Disable();
        actionZ.Disable();
#endif
    }

    // Функции, которые вызываются (ваш код здесь)
    private void OnActionQ(InputAction.CallbackContext context)
    {
        Ended?.Invoke(GameOutcome.Escaped);
    }

    private void OnActionA(InputAction.CallbackContext context)
    {
        Ended?.Invoke(GameOutcome.WrongBugs);
    }

    private void OnActionZ(InputAction.CallbackContext context)
    {
        Ended?.Invoke(GameOutcome.Timeout);
    }
}