using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PointOfOrigin
{
    /// <summary>One level as tools/levels.nx writes it into StreamingAssets/levels.json.</summary>
    [Serializable]
    public class Level
    {
        public string name;
        public string hint;
        public string tip;      // shown after the first failed attempt
        public string intro;    // the chapter card before the level
        public string outro;    // the line after the origin is found
        public string rule;
        public int w;
        public int h;
        public int birth;
        public int survive;
        public int steps;
        public int seeds;
        public int solutions;   // seed placements that grow the target; -1 when not counted
        public string rock;     // w*h chars, '#' rock
        public string target;   // w*h chars, '#' alive
        public string origins;  // "x,y;x,y" the designer's answer
        public string start;    // "x,y" where the wanderer wakes, or empty for the centre
        public string pickups;  // "x,y;x,y" seeds lying in the world; empty means the seeds are carried from the start
        public string embers;   // "x,y,h;x,y,v" embers patrolling a row (h) or a column (v)
        public string exit;     // "x,y" the door that opens once the growth is exact
        public string acid;     // "x,y;x,y" pools: not solid, deadly (rock to the automaton)
        public string spikes;   // "x,y;x,y" deadly to touch, open to the automaton
        public string spouts;   // "x,y;x,y" vents in the rock that throw flame two cells up on a timer
        public string crumble;  // "x,y;x,y" rock that falls away shortly after you stand on it
        public string says;     // "trigger|line||trigger|line": what the lantern says at wake, plant, grow, found, die

        public List<(string trigger, string line)> Lines()
        {
            var list = new List<(string, string)>();
            if (string.IsNullOrEmpty(says)) return list;
            foreach (var entry in says.Split(new[] { "||" }, StringSplitOptions.None))
            {
                int bar = entry.IndexOf('|');
                if (bar > 0) list.Add((entry.Substring(0, bar).Trim(), entry.Substring(bar + 1).Trim()));
            }
            return list;
        }

        public byte[] RockCells() => Cells(rock, Sim.Rock);
        public List<Vector2Int> AcidCells() => ParseCells(acid);
        public List<Vector2Int> SpikeCells() => ParseCells(spikes);
        public List<Vector2Int> SpoutCells() => ParseCells(spouts);
        public List<Vector2Int> CrumbleCells() => ParseCells(crumble);

        public Vector2Int? ExitCell()
        {
            if (string.IsNullOrEmpty(exit)) return null;
            var xy = exit.Split(',');
            if (xy.Length == 2 && int.TryParse(xy[0], out var x) && int.TryParse(xy[1], out var y))
                return new Vector2Int(x, y);
            return null;
        }

        public List<Vector2Int> PickupCells() => ParseCells(pickups);

        public List<(Vector2Int cell, bool vertical)> EmberSpecs()
        {
            var list = new List<(Vector2Int, bool)>();
            if (string.IsNullOrEmpty(embers)) return list;
            foreach (var part in embers.Split(';'))
            {
                var f = part.Split(',');
                if (f.Length == 3 && int.TryParse(f[0], out var x) && int.TryParse(f[1], out var y))
                    list.Add((new Vector2Int(x, y), f[2].Trim() == "v"));
            }
            return list;
        }

        static List<Vector2Int> ParseCells(string text)
        {
            var list = new List<Vector2Int>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (var part in text.Split(';'))
            {
                var xy = part.Split(',');
                if (xy.Length == 2 && int.TryParse(xy[0], out var x) && int.TryParse(xy[1], out var y))
                    list.Add(new Vector2Int(x, y));
            }
            return list;
        }

        public Vector2Int? StartCell()
        {
            if (string.IsNullOrEmpty(start)) return null;
            var xy = start.Split(',');
            if (xy.Length == 2 && int.TryParse(xy[0], out var x) && int.TryParse(xy[1], out var y))
                return new Vector2Int(x, y);
            return null;
        }
        public byte[] TargetCells() => Cells(target, Sim.Alive);

        byte[] Cells(string text, byte value)
        {
            var cells = new byte[w * h];
            if (text == null) return cells;
            for (int i = 0; i < cells.Length && i < text.Length; i++)
                cells[i] = text[i] == '#' ? value : Sim.Dead;
            return cells;
        }

        public List<Vector2Int> OriginCells()
        {
            var list = new List<Vector2Int>();
            if (string.IsNullOrEmpty(origins)) return list;
            foreach (var part in origins.Split(';'))
            {
                var xy = part.Split(',');
                if (xy.Length == 2 && int.TryParse(xy[0], out var x) && int.TryParse(xy[1], out var y))
                    list.Add(new Vector2Int(x, y));
            }
            return list;
        }
    }

    [Serializable]
    public class LevelSet
    {
        public Level[] levels;
    }

    public static class Levels
    {
        public static string Path => System.IO.Path.Combine(Application.streamingAssetsPath, "levels.json");

        /// <summary>Load levels.json; throws with a readable message when it is missing or empty.</summary>
        public static LevelSet Load()
        {
            var path = Path;
            if (!File.Exists(path))
                throw new FileNotFoundException($"No levels at {path}. Run `nx run build.nx` in the repository root.");
            var set = JsonUtility.FromJson<LevelSet>(File.ReadAllText(path));
            if (set?.levels == null || set.levels.Length == 0)
                throw new InvalidDataException($"{path} holds no levels.");
            foreach (var lv in set.levels)
            {
                if (lv.w <= 0 || lv.h <= 0 || lv.target == null || lv.target.Length != lv.w * lv.h)
                    throw new InvalidDataException($"Level '{lv.name}' has a bad size or target.");
            }
            return set;
        }

        /// <summary>A Life-like rule in plain words, e.g. "A dead cell is born beside exactly 1 living neighbour. Every living cell survives."</summary>
        public static string Describe(int birth, int survive)
        {
            string born = Neighbours(birth);
            string live;
            if (survive == 511) live = "Every living cell survives.";
            else if (survive == 0) live = "Nothing survives past one generation.";
            else live = $"A living cell survives beside {Neighbours(survive)}.";
            return $"A dead cell is born beside {born}. {live}";
        }

        static string Neighbours(int mask)
        {
            if (mask == 0b010101010) return "an odd number of living neighbours";
            if (mask == 0b101010101) return "an even number of living neighbours";
            var counts = new List<int>();
            for (int n = 0; n <= 8; n++)
                if ((mask & (1 << n)) != 0) counts.Add(n);
            if (counts.Count == 0) return "no living neighbours at all";
            if (counts.Count == 1) return $"exactly {counts[0]} living neighbour{(counts[0] == 1 ? "" : "s")}";
            string list = string.Join(", ", counts.GetRange(0, counts.Count - 1)) + " or " + counts[counts.Count - 1];
            return $"{list} living neighbours";
        }
    }
}
