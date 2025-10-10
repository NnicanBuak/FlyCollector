using System.Linq;
using UnityEngine;
using TMPro;

public class TargetBugsList : MonoBehaviour
{
    [Header("Вывод")]
    [SerializeField] private TextMeshPro leftText;
    [SerializeField] private TextMeshPro rightText;

    [Header("Формат")]
    [Tooltip("Процент к базовому размеру шрифта колонки для описания.")]
    [SerializeField, Min(1)] private int descriptionSizePercent = 125;

    [Tooltip("Если описание заняло 1 строку — добавить пустую строку, чтобы каждый пункт был в 3 строки.")]
    [SerializeField] private bool padToThreeLines = true;

    private void Awake()
    {
        if (leftText)  leftText.richText  = true;
        if (rightText) rightText.richText = true;
    }

    private void OnEnable()
    {
        if (TargetBugsRuntime.Instance)
            TargetBugsRuntime.Instance.TargetsChanged += Rebuild;
        Rebuild();
    }

    private void OnDisable()
    {
        if (TargetBugsRuntime.Instance)
            TargetBugsRuntime.Instance.TargetsChanged -= Rebuild;
    }

    private void Rebuild()
    {
        var rt = TargetBugsRuntime.Instance;
        if (rt == null || rt.Targets == null || rt.Targets.Count == 0)
        {
            SetText("", "");
            return;
        }

        var pairs = rt.Targets.Select(key =>
        {
            if (rt.TryGetMeta(key, out var t, out var d)) return (title: t, desc: d);
            else                                          return (title: key, desc: "(no description)");
        }).ToList();

        int half = (pairs.Count + 1) / 2;
        var leftItems  = pairs.Take(half).ToList();
        var rightItems = pairs.Skip(half).ToList();

        var left  = string.Join("\n", leftItems .Select(p => FormatItem(p.title, p.desc, leftText)).ToArray());
        var right = string.Join("\n", rightItems.Select(p => FormatItem(p.title, p.desc, rightText)).ToArray());

        SetText(left, right);
    }

    private string FormatItem(string title, string desc, TMP_Text probe)
    {

        string titleCapsItalic = $"<i>{(title ?? string.Empty).ToUpperInvariant()}</i>";


        string descCaps = (desc ?? string.Empty).ToUpperInvariant();
        string formattedDesc = $"<size={descriptionSizePercent}%><b>{descCaps}</b></size>";


        bool needExtraBlank = padToThreeLines && GetLineCount(formattedDesc, probe) == 1;
        return needExtraBlank ? $"{titleCapsItalic}\n{formattedDesc}\n\n"
                              : $"{titleCapsItalic}\n{formattedDesc}\n";
    }


    private int GetLineCount(string richText, TMP_Text probe)
    {
        if (probe == null || string.IsNullOrEmpty(richText)) return 1;

        string oldText = probe.text;
        bool oldAuto = probe.enableAutoSizing;
        var oldOverflow = probe.overflowMode;

        probe.enableAutoSizing = false;
        probe.overflowMode = TextOverflowModes.Overflow;
        probe.text = richText;
        probe.ForceMeshUpdate();

        int lines = Mathf.Max(1, probe.textInfo?.lineCount ?? 1);

        probe.text = oldText;
        probe.enableAutoSizing = oldAuto;
        probe.overflowMode = oldOverflow;

        return lines;
    }

    private void SetText(string left, string right)
    {
        if (leftText)  leftText.text  = left;
        if (rightText) rightText.text = right;
    }
}
