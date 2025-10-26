using System;
using System.Collections.Generic;

[Serializable]
public class GameResult
{
    public GameOutcome Outcome;
    public List<string> Caught = new List<string>();
    public List<string> Targets = new List<string>();
    public List<string> BugsToSpawn = new List<string>();
    public int Total;
    public int Correct;
    public int Wrong;
}
