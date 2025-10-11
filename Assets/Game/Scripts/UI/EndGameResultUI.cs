using Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndGameResultUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text detailsText;
    [SerializeField] private Image outcomeIcon;
    [SerializeField] private Sprite victorySprite;
    [SerializeField] private Sprite wrongBugsSprite;
    [SerializeField] private Sprite timeoutSprite;

    [Header("Keys in GameSceneManager persistent data")]
    [SerializeField] private string outcomeKey = "gameOutcome";
    [SerializeField] private string totalCaughtKey = "totalCaught";
    [SerializeField] private string wrongCountKey = "wrongCount";

    private void Awake()
    {
        if (titleText == null)  titleText  = GetComponentInChildren<TMP_Text>();
        if (detailsText == null) detailsText = titleText;
    }

    private void Start()
    {
        GameOutcome outcome = GameOutcome.Victory;
        int total = 0, wrong = 0;

        var gsm = GameSceneManager.Instance;
        if (gsm != null)
        {
            if (gsm.HasPersistentData(outcomeKey))
                outcome = gsm.GetPersistentData<GameOutcome>(outcomeKey, GameOutcome.Victory);
            total = gsm.GetPersistentData<int>(totalCaughtKey, 0);
            wrong = gsm.GetPersistentData<int>(wrongCountKey, 0);
        }

        Apply(outcome, total, wrong);
    }

    private void Apply(GameOutcome outcome, int total, int wrong)
    {
        if (titleText)
            titleText.text = outcome switch
            {
                GameOutcome.Victory   => "Победа!",
                GameOutcome.WrongBugs => "Неверные жуки",
                GameOutcome.Timeout   => "Время вышло",
                _ => "Результат"
            };

        if (detailsText)
            detailsText.text = $"Поймано: {total}\nОшибок: {wrong}";

        if (outcomeIcon)
        {
            outcomeIcon.sprite = outcome switch
            {
                GameOutcome.Victory   => victorySprite,
                GameOutcome.WrongBugs => wrongBugsSprite,
                GameOutcome.Timeout   => timeoutSprite,
                _ => null
            };
            outcomeIcon.enabled = outcomeIcon.sprite != null;
        }
    }
}
