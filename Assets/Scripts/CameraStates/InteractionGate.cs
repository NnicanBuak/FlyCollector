public static class InteractionGate
{
    private static bool suppressOnce;

    public static void SuppressNextAutoStart() => suppressOnce = true;

    public static bool Consume()
    {
        if (!suppressOnce) return false;
        suppressOnce = false;
        return true;
    }
}