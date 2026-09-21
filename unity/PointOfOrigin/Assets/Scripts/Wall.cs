using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace PointOfOrigin
{
    /// <summary>
    /// The wall of initials: everyone who read the wall in the chamber and sent
    /// their letters. The list ships in StreamingAssets, is refreshed from the
    /// repository at start-up when the network allows, and is cached between runs.
    /// Your own initials are pressed in locally the moment you choose them.
    ///
    /// Sharing goes through a GitHub issue, so it takes a signed-in GitHub account
    /// and the wall keeps one line per account. The issue never carries the word
    /// itself, only a proof: a hash of the word and the account name, which the
    /// repository's workflow checks against a secret before it presses the
    /// initials in.
    /// </summary>
    public static class Wall
    {
        public const string RemoteUrl = "https://raw.githubusercontent.com/Londopy/point-of-origin/main/docs/initials.txt";
        public const string IssueUrl = "https://github.com/Londopy/point-of-origin/issues/new";
        const string KeyCache = "po.wall.cache";
        const string KeyMine = "po.wall.mine";
        const string KeySolved = "po.wall.solved";
        const string KeyWord = "po.wall.word";
        const string KeyUser = "po.wall.user";

        public static readonly List<string> Names = new List<string>();
        public static bool Fetched { get; private set; }

        public static string Mine => PlayerPrefs.GetString(KeyMine, "");
        public static bool Solved => PlayerPrefs.GetInt(KeySolved, 0) != 0;
        public static string Word => PlayerPrefs.GetString(KeyWord, "");
        public static string User => PlayerPrefs.GetString(KeyUser, "");

        public static void Load()
        {
            Names.Clear();
#if UNITY_WEBGL && !UNITY_EDITOR
            Merge(Resources.Load<TextAsset>("initials")?.text);
#else
            var shipped = Path.Combine(Application.streamingAssetsPath, "initials.txt");
            if (File.Exists(shipped)) Merge(File.ReadAllText(shipped));
#endif
            Merge(PlayerPrefs.GetString(KeyCache, ""));
            if (Mine.Length > 0) Merge(Mine);
        }

        /// <summary>Add well-formed entries (1 to 3 letters or digits, the first word of each line) that are not already there.</summary>
        static void Merge(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var raw in text.Split('\n'))
            {
                var first = raw.Trim().Split(' ')[0];
                var s = Clean(first);
                if (s.Length > 0 && !Names.Contains(s)) Names.Add(s);
            }
        }

        public static string Clean(string raw)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in raw.Trim().ToUpperInvariant())
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) { sb.Append(c); if (sb.Length == 3) break; }
            return sb.ToString();
        }

        /// <summary>A GitHub account name as GitHub allows them: letters, digits and hyphens, up to 39.</summary>
        public static string CleanUser(string raw)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in raw.Trim())
                if (char.IsLetterOrDigit(c) || c == '-') { sb.Append(c); if (sb.Length == 39) break; }
            return sb.ToString();
        }

        public static IEnumerator Fetch()
        {
            using (var req = UnityWebRequest.Get(RemoteUrl))
            {
                req.timeout = 6;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    PlayerPrefs.SetString(KeyCache, req.downloadHandler.text);
                    PlayerPrefs.Save();
                    Merge(req.downloadHandler.text);
                    Fetched = true;
                }
            }
        }

        public static string Sha256Hex(string text)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
                var sb = new System.Text.StringBuilder();
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>What the game checks a typed word against.</summary>
        public static string AnswerHash(string word) => Sha256Hex("point-of-origin:" + word.Trim().ToLowerInvariant());

        /// <summary>What goes in the issue: the word and the account, hashed together, so the word itself never leaves the game.</summary>
        public static string Proof(string word, string user) =>
            Sha256Hex("point-of-origin:" + word.Trim().ToLowerInvariant() + ":" + user.Trim().ToLowerInvariant());

        public static void MarkSolved(string word)
        {
            PlayerPrefs.SetInt(KeySolved, 1);
            PlayerPrefs.SetString(KeyWord, word.Trim().ToLowerInvariant());
            PlayerPrefs.Save();
        }

        /// <summary>Press your own initials into the wall, locally, now.</summary>
        public static void SetMine(string initials)
        {
            var s = Clean(initials);
            if (s.Length == 0) return;
            PlayerPrefs.SetString(KeyMine, s);
            PlayerPrefs.Save();
            if (!Names.Contains(s)) Names.Add(s);
        }

        public static void SetUser(string user)
        {
            PlayerPrefs.SetString(KeyUser, CleanUser(user));
            PlayerPrefs.Save();
        }

        /// <summary>The GitHub issue that the repository's workflow turns into a line in the shared list.</summary>
        public static string ShareUrl(string initials, string user)
        {
            string title = Uri.EscapeDataString("initials: " + Clean(initials));
            string body = Uri.EscapeDataString(
                "proof: " + Proof(Word, user) + "\n\n" +
                "Read in the chamber of Two Echoes, submitted as " + CleanUser(user) + ". " +
                "The wall checks the proof against this account, adds the initials to docs/initials.txt and closes this.");
            return $"{IssueUrl}?title={title}&body={body}&labels=wall";
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(KeyMine);
            PlayerPrefs.DeleteKey(KeySolved);
            PlayerPrefs.DeleteKey(KeyWord);
            PlayerPrefs.DeleteKey(KeyUser);
            PlayerPrefs.Save();
        }
    }
}
