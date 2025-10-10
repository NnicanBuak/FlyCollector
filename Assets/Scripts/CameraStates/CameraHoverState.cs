using UnityEngine;

public class CameraHoverState
{
    private IInspectable hoveredInspectable;
    private IInteractable hoveredInteractable;
    private IFocusable hoveredFocusable;
    private IPushable hoveredPushable;

    public void UpdateInspectable(IInspectable newInspectable)
    {
        if (newInspectable != hoveredInspectable)
        {
            hoveredInspectable?.OnHoverExit();
            hoveredInspectable = newInspectable;
            hoveredInspectable?.OnHoverEnter();
        }
    }

    public void UpdateInteractable(IInteractable newInteractable)
    {
        if (newInteractable != hoveredInteractable)
        {
            hoveredInteractable?.OnHoverExit();
            hoveredInteractable = newInteractable;
            hoveredInteractable?.OnHoverEnter();
        }
    }

    public void UpdateFocusable(IFocusable newFocusable)
    {
        if (newFocusable != hoveredFocusable)
        {
            hoveredFocusable?.OnFocusHoverExit();
            hoveredFocusable = newFocusable;
            hoveredFocusable?.OnFocusHoverEnter();
        }
    }

    public void UpdatePushable(IPushable newPushable)
    {
        if (newPushable != hoveredPushable)
        {
            hoveredPushable?.OnPushHoverExit();
            hoveredPushable = newPushable;
            hoveredPushable?.OnPushHoverEnter();
        }
    }

    public void ClearAll()
    {
        hoveredInspectable?.OnHoverExit();
        hoveredInspectable = null;

        hoveredInteractable?.OnHoverExit();
        hoveredInteractable = null;

        hoveredFocusable?.OnFocusHoverExit();
        hoveredFocusable = null;

        hoveredPushable?.OnPushHoverExit();
        hoveredPushable = null;
    }

    public IInspectable Inspectable => hoveredInspectable;
    public IInteractable Interactable => hoveredInteractable;
    public IFocusable Focusable => hoveredFocusable;
    public IPushable Pushable => hoveredPushable;
}
