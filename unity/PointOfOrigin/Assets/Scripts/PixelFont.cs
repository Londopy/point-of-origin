using System.Collections.Generic;
using UnityEngine;

namespace PointOfOrigin
{
    /// <summary>A four-by-five pixel alphabet, for initials pressed into rock and for the wall's cipher.</summary>
    public static class PixelFont
    {
        static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            { 'A', new[] { ".##.", "#..#", "####", "#..#", "#..#" } }, { 'B', new[] { "###.", "#..#", "###.", "#..#", "###." } },
            { 'C', new[] { ".###", "#...", "#...", "#...", ".###" } }, { 'D', new[] { "###.", "#..#", "#..#", "#..#", "###." } },
            { 'E', new[] { "####", "#...", "###.", "#...", "####" } }, { 'F', new[] { "####", "#...", "###.", "#...", "#..." } },
            { 'G', new[] { ".###", "#...", "#.##", "#..#", ".###" } }, { 'H', new[] { "#..#", "#..#", "####", "#..#", "#..#" } },
            { 'I', new[] { "###.", ".#..", ".#..", ".#..", "###." } }, { 'J', new[] { "####", "..#.", "..#.", "#.#.", ".#.." } },
            { 'K', new[] { "#..#", "#.#.", "##..", "#.#.", "#..#" } }, { 'L', new[] { "#...", "#...", "#...", "#...", "####" } },
            { 'M', new[] { "#..#", "####", "####", "#..#", "#..#" } }, { 'N', new[] { "#..#", "##.#", "#.##", "#..#", "#..#" } },
            { 'O', new[] { ".##.", "#..#", "#..#", "#..#", ".##." } }, { 'P', new[] { "###.", "#..#", "###.", "#...", "#..." } },
            { 'Q', new[] { ".##.", "#..#", "#..#", "#.##", ".###" } }, { 'R', new[] { "###.", "#..#", "###.", "#.#.", "#..#" } },
            { 'S', new[] { ".###", "#...", ".##.", "...#", "###." } }, { 'T', new[] { "####", ".#..", ".#..", ".#..", ".#.." } },
            { 'U', new[] { "#..#", "#..#", "#..#", "#..#", ".##." } }, { 'V', new[] { "#..#", "#..#", "#..#", ".##.", ".##." } },
            { 'W', new[] { "#..#", "#..#", "####", "####", "#..#" } }, { 'X', new[] { "#..#", "#..#", ".##.", "#..#", "#..#" } },
            { 'Y', new[] { "#..#", "#..#", ".##.", ".#..", ".#.." } }, { 'Z', new[] { "####", "..#.", ".#..", "#...", "####" } },
            { '0', new[] { "####", "#..#", "#..#", "#..#", "####" } }, { '1', new[] { ".#..", "##..", ".#..", ".#..", "###." } },
            { '2', new[] { "###.", "...#", ".##.", "#...", "####" } }, { '3', new[] { "###.", "...#", ".##.", "...#", "###." } },
            { '4', new[] { "#..#", "#..#", "####", "...#", "...#" } }, { '5', new[] { "####", "#...", "###.", "...#", "###." } },
            { '6', new[] { ".###", "#...", "###.", "#..#", ".##." } }, { '7', new[] { "####", "...#", "..#.", ".#..", ".#.." } },
            { '8', new[] { ".##.", "#..#", ".##.", "#..#", ".##." } }, { '9', new[] { ".##.", "#..#", ".###", "...#", "###." } },
        };

        public const int Height = 5;

        /// <summary>The lit cells of a string laid out left to right, `gap` empty columns between letters, top-left at (0,0).</summary>
        public static List<Vector2Int> Cells(string text, int gap = 1)
        {
            var cells = new List<Vector2Int>();
            int x0 = 0;
            foreach (var raw in text.ToUpperInvariant())
            {
                if (!Glyphs.TryGetValue(raw, out var g)) { x0 += 2 + gap; continue; }
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < g[y].Length; x++)
                        if (g[y][x] == '#') cells.Add(new Vector2Int(x0 + x, y));
                x0 += g[0].Length + gap;
            }
            return cells;
        }

        public static int Width(string text, int gap = 1)
        {
            int w = 0;
            foreach (var raw in text.ToUpperInvariant()) w += (Glyphs.ContainsKey(raw) ? Glyphs[raw][0].Length : 2) + gap;
            return Mathf.Max(0, w - gap);
        }
    }
}
