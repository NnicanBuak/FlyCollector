using System;

// Simple static event bus for the Inspect flip action, so UI and logic can communicate
public static class InspectFlip
{
    public static Action OnClicked;
}
