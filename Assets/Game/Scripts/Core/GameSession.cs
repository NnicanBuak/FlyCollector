public static class GameSession
{
    public static GameResult CurrentResult { get; private set; }

    public static void SetResult(GameResult result) => CurrentResult = result;
    public static void Clear() => CurrentResult = null;
}
