using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Persists player scores locally with PlayerPrefs (stored as JSON) and returns a sorted
/// top-N leaderboard. Fully decoupled from UI and game logic.
/// </summary>
public class LeaderboardManager : MonoBehaviour
{
    [Serializable]
    public struct ScoreEntry
    {
        public string playerName;
        public int saves;
    }

    // JsonUtility cannot serialize a bare List, so it is wrapped.
    [Serializable]
    private class ScoreTable
    {
        public List<ScoreEntry> entries = new List<ScoreEntry>();
    }

    [SerializeField] private string prefsKey = "goalkeeper_leaderboard";

    /// <summary>Adds a result and saves immediately.</summary>
    public void AddScore(string playerName, int saves)
    {
        var table = Load();
        table.entries.Add(new ScoreEntry
        {
            playerName = string.IsNullOrWhiteSpace(playerName) ? "Anonymous" : playerName.Trim(),
            saves = saves
        });
        Save(table);
    }

    /// <summary>Returns the top <paramref name="count"/> entries, highest saves first.</summary>
    public List<ScoreEntry> GetTop(int count)
    {
        return Load().entries
            .OrderByDescending(e => e.saves)
            .Take(Mathf.Max(0, count))
            .ToList();
    }

    /// <summary>Wipes all stored scores.</summary>
    public void Clear()
    {
        PlayerPrefs.DeleteKey(prefsKey);
        PlayerPrefs.Save();
    }

    private ScoreTable Load()
    {
        if (!PlayerPrefs.HasKey(prefsKey)) return new ScoreTable();
        var json = PlayerPrefs.GetString(prefsKey);
        if (string.IsNullOrEmpty(json)) return new ScoreTable();
        var table = JsonUtility.FromJson<ScoreTable>(json);
        return table ?? new ScoreTable();
    }

    private void Save(ScoreTable table)
    {
        PlayerPrefs.SetString(prefsKey, JsonUtility.ToJson(table));
        PlayerPrefs.Save();
    }
}
