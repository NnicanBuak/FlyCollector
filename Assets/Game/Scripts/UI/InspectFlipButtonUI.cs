using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class InspectFlipButtonUI : MonoBehaviour
{
    private Button btn;
    private Image img;

    void Awake()
    {
        btn = GetComponent<Button>();
        img = GetComponent<Image>();
        if (img != null)
        {
            img.raycastTarget = true;
        }

        if (btn != null)
        {
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(InvokeFlip);
        }
    }

    private void OnDestroy()
    {
        if (btn != null)
        {
            btn.onClick.RemoveListener(InvokeFlip);
        }
    }

    private void InvokeFlip()
    {
        InspectFlip.OnClicked?.Invoke();
    }
}

