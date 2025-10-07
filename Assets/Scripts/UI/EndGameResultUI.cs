// EndGameResultsUI.cs
// Скрипт для "сцены завершения": показывает иконки банок по числу пойманных,
// и выводит количество "неправильных" (тех, кого не было в цели).
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndGameResultsUI : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Родитель (Grid/HorizontalLayoutGroup) для иконок-банок")]
    [SerializeField] private Transform jarsParent;

    [Tooltip("Префаб одной иконки банки (например, Image внутри пустого GO)")]
    [SerializeField] private GameObject jarIconPrefab;

    [Tooltip("Иконка банка-спрайт (если префабу нужен спрайт)")]
    [SerializeField] private Sprite jarSprite;

    [Header("Тексты")]
    [SerializeField] private TextMeshProUGUI totalCaughtText;
    [SerializeField] private TextMeshProUGUI wrongCountText;

    [Header("Опции отображения")]
    [Tooltip("Очищать контейнер перед созданием иконок")]
    [SerializeField] private bool clearBeforeBuild = true;

    private void Start()
    {
        Build();
    }

    public void Build()
    {
        var targets = TargetBugsRuntime.Instance ? TargetBugsRuntime.Instance.Targets : new List<string>();
        var caught  = CaughtBugsRuntime.Instance ? CaughtBugsRuntime.Instance.Caught  : new List<string>();

        var targetSet = new HashSet<string>(targets.Select(TargetBugsRuntime.NormalizeKey));
        var caughtList = caught.Select(TargetBugsRuntime.NormalizeKey).ToList();

        int totalCaught = caughtList.Count;
        int wrong = caughtList.Count(c => !targetSet.Contains(c));

        // Иконки-банки по числу пойманных
        if (jarsParent && jarIconPrefab)
        {
            if (clearBeforeBuild)
            {
                for (int i = jarsParent.childCount - 1; i >= 0; i--)
                    Destroy(jarsParent.GetChild(i).gameObject);
            }

            for (int i = 0; i < totalCaught; i++)
            {
                var go = Instantiate(jarIconPrefab, jarsParent);
                var img = go.GetComponentInChildren<Image>();
                if (img && jarSprite) img.sprite = jarSprite;
                // можно добавить подсказку/тултип с именем
                // var tt = go.GetComponent<Tooltip>(); tt?.SetText(caughtList[i]);
            }
        }

        if (totalCaughtText) totalCaughtText.text = $"Собрано жуков: {totalCaught}";
        if (wrongCountText)  wrongCountText.text  = $"Неправильных жуков: {wrong}";
    }
}
