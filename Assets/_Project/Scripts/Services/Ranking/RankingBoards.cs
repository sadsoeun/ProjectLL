using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyGame
{
    public enum RankingStatus
    {
        Ok,       // online boards answered
        Offline,  // Steam is not available: only the player's own record is shown
        Failed,   // Steam is running but the request failed
    }

    public struct RankingEntry
    {
        public int rank;      // 1-based global rank, 0 = not ranked
        public string name;
        public int score;     // board score (see RankingBoards)
        public bool isSelf;
    }

    public sealed class RankingResult
    {
        public RankingStatus status;
        public readonly List<RankingEntry> top = new List<RankingEntry>();
        public bool hasSelf;
        public RankingEntry self;
        public int totalEntries;
    }

    /// <summary>
    /// Board names and score encodings shared by the ranking service, its backend and the popup.
    /// Stage board: clear time in milliseconds, lower is better.
    /// Overall board: one number, higher is better = cleared stages first, then the shorter sum of best times:
    /// clears * 10,000,000 + (9,999,999 - total centiseconds). The sum is capped at 27.7 hours.
    /// </summary>
    public static class RankingBoards
    {
        /// <summary>Prefix that keeps the boards apart from other games while the shared test App ID is used.</summary>
        public const string Prefix = "LeafLeap_";
        public const string Overall = Prefix + "overall";
        public const int NicknameMaxLength = 12;

        private const int OverallBase = 10_000_000;

        public static string Stage(string stageId) => Prefix + "stage_" + stageId;

        public static int StageScore(float seconds) =>
            (int)Mathf.Clamp(Mathf.Round(seconds * 1000f), 1f, int.MaxValue - 1);

        public static int OverallScore(int clears, float totalSeconds)
        {
            int centis = (int)Mathf.Clamp(Mathf.Round(totalSeconds * 100f), 0f, OverallBase - 1);
            return Mathf.Clamp(clears, 0, 200) * OverallBase + (OverallBase - 1 - centis);
        }

        public static void DecodeOverall(int score, out int clears, out float totalSeconds)
        {
            clears = Mathf.Max(0, score) / OverallBase;
            totalSeconds = (OverallBase - 1 - Mathf.Max(0, score) % OverallBase) / 100f;
        }

        /// <summary>Cleared stages of the database and the sum of their best times.</summary>
        public static void Summarize(SaveData save, StageDatabase db, out int clears, out float totalSeconds)
        {
            clears = 0;
            totalSeconds = 0f;
            if (save == null || db == null)
                return;
            foreach (var stage in db.Stages)
            {
                if (stage == null)
                    continue;
                var record = save.FindStage(stage.StageId);
                if (record == null || !record.cleared || record.bestTime <= 0f)
                    continue;
                clears++;
                totalSeconds += record.bestTime;
            }
        }

        /// <summary>m:ss.cc (h:mm:ss.cc from one hour).</summary>
        public static string FormatTime(float seconds)
        {
            int centis = Mathf.Max(0, Mathf.RoundToInt(seconds * 100f));
            int h = centis / 360000, m = centis / 6000 % 60, s = centis / 100 % 60, c = centis % 100;
            return h > 0 ? $"{h}:{m:00}:{s:00}.{c:00}" : $"{m}:{s:00}.{c:00}";
        }

        /// <summary>Trims, drops control characters and rich-text brackets, limits the length.</summary>
        public static string CleanNickname(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;
            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (char ch in raw.Trim())
            {
                if (char.IsControl(ch) || ch == '<' || ch == '>')
                    continue;
                sb.Append(ch);
                if (sb.Length >= NicknameMaxLength)
                    break;
            }
            return sb.ToString().Trim();
        }

        // The nickname travels in the score details of a board entry (int32 array): a tagged length, then two UTF-16 units per int.
        private const int NameTag = 0x4E4B0000;

        public static int[] PackNickname(string name)
        {
            name = CleanNickname(name);
            var data = new int[1 + (name.Length + 1) / 2];
            data[0] = NameTag | name.Length;
            for (int i = 0; i < name.Length; i++)
                data[1 + i / 2] |= name[i] << (i % 2 == 0 ? 0 : 16);
            return data;
        }

        public static string UnpackNickname(int[] data, int count)
        {
            if (data == null || count < 1 || (data[0] & unchecked((int)0xFFFF0000)) != NameTag)
                return null;
            int length = Math.Min(data[0] & 0xFFFF, NicknameMaxLength);
            if (count < 1 + (length + 1) / 2)
                return null;
            var chars = new char[length];
            for (int i = 0; i < length; i++)
                chars[i] = (char)((data[1 + i / 2] >> (i % 2 == 0 ? 0 : 16)) & 0xFFFF);
            string name = CleanNickname(new string(chars));
            return name.Length > 0 ? name : null;
        }
    }
}
