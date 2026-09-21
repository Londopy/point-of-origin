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
        public string rule;
        public int w;
        public int h;
        public int birth;
        public int survive;
        public int steps;
        public int seeds;
        public string rock;     // w*h chars, '#' rock
        public string target;   // w*h chars, '#' alive
        public string origins;  // "x,y;x,y" the designer's answer

        public byte[] RockCells() => Cells(rock, Sim.Rock);
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
    }
}
