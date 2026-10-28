using System;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>Local author identity; existing imported IDs are never rewritten.</summary>
public static class CollaborationIdentity
{
    const string UserKey = "rwrme.collaboration.userId";
    const string LocalKey = "rwrme.collaboration.localId";
    public static string UserId => PlayerPrefs.GetString(UserKey, "");
    public static bool IsValidUserId(string value) => value != null && Regex.IsMatch(value, @"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,63}\z");

    public static void SetUserId(string value)
    {
        if (!IsValidUserId(value)) throw new ArgumentException("User ID: 1-64 ASCII letters, digits, dots, underscores or hyphens; start with a letter or digit.");
        PlayerPrefs.SetString(UserKey, value);
        PlayerPrefs.Save();
    }

    public static string NewItemSuffix()
    {
        string author = UserId;
        if (!IsValidUserId(author))
        {
            author = PlayerPrefs.GetString(LocalKey, "");
            if (!IsValidUserId(author))
            {
                author = "local-" + Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(LocalKey, author);
                PlayerPrefs.Save();
            }
        }
        return "_u_" + author + "_" + Guid.NewGuid().ToString("N");
    }
}
