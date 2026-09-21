using System;

namespace PointOfOrigin
{
    /// <summary>
    /// The automaton of native/sim/sim.odin, line for line, in C#: for the platforms where the Odin
    /// core cannot ship as a native library (the browser). Rock never changes and never counts as a
    /// neighbour; ages count the generations a cell has been alive. The Odin core stays the reference:
    /// the level compiler grows every target with it, and the game checks this copy against it.
    /// </summary>
    public sealed class SimCore
    {
        public const byte Dead = 0;
        public const byte Alive = 1;
        public const byte Rock = 2;
        const ushort MaxAge = 65535;

        public readonly int W;
        public readonly int H;
        byte[] cells, nextCells;
        ushort[] ages, nextAges;
        uint birth = 1u << 3;                       // Conway's Life until told otherwise
        uint survive = (1u << 2) | (1u << 3);
        public int Generation { get; private set; }

        public SimCore(int w, int h)
        {
            if (w <= 0 || h <= 0 || w > 1024 || h > 1024) throw new ArgumentOutOfRangeException($"SimCore({w}, {h})");
            W = w;
            H = h;
            int n = w * h;
            cells = new byte[n];
            ages = new ushort[n];
            nextCells = new byte[n];
            nextAges = new ushort[n];
        }

        public void SetRule(uint birthMask, uint surviveMask)
        {
            birth = birthMask;
            survive = surviveMask;
        }

        /// <summary>Kill every living cell, keep the rock, rewind the generation counter.</summary>
        public void ClearLife()
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == Alive) cells[i] = Dead;
                ages[i] = 0;
            }
            Generation = 0;
        }

        /// <summary>Everything dead, no rock.</summary>
        public void ClearAll()
        {
            Array.Clear(cells, 0, cells.Length);
            Array.Clear(ages, 0, ages.Length);
            Generation = 0;
        }

        bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;

        public void Set(int x, int y, byte v)
        {
            if (!InBounds(x, y)) return;
            int i = y * W + x;
            cells[i] = v;
            ages[i] = (ushort)(v == Alive ? 1 : 0);
        }

        public byte Get(int x, int y) => InBounds(x, y) ? cells[y * W + x] : Dead;

        /// <summary>Replace the whole grid (row-major, w*h values) and rewind to generation 0.</summary>
        public void Load(byte[] src)
        {
            if (src == null || src.Length == 0) return;
            int n = Math.Min(src.Length, cells.Length);
            for (int i = 0; i < n; i++)
            {
                cells[i] = src[i];
                ages[i] = (ushort)(src[i] == Alive ? 1 : 0);
            }
            Generation = 0;
        }

        int Neighbours(int x, int y)
        {
            int count = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                    if (cells[ny * W + nx] == Alive) count++;
                }
            return count;
        }

        void StepOnce()
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    byte c = cells[i];
                    if (c == Rock)
                    {
                        nextCells[i] = Rock;
                        nextAges[i] = 0;
                        continue;
                    }
                    uint mask = 1u << Neighbours(x, y);
                    if (c == Alive)
                    {
                        if ((survive & mask) != 0)
                        {
                            nextCells[i] = Alive;
                            nextAges[i] = ages[i] < MaxAge ? (ushort)(ages[i] + 1) : MaxAge;
                        }
                        else
                        {
                            nextCells[i] = Dead;
                            nextAges[i] = 0;
                        }
                    }
                    else if ((birth & mask) != 0)
                    {
                        nextCells[i] = Alive;
                        nextAges[i] = 1;
                    }
                    else
                    {
                        nextCells[i] = Dead;
                        nextAges[i] = 0;
                    }
                }
            (cells, nextCells) = (nextCells, cells);
            (ages, nextAges) = (nextAges, ages);
            Generation++;
        }

        /// <summary>Advance n generations; returns the generation reached.</summary>
        public int Step(int n)
        {
            for (int k = 0; k < n; k++) StepOnce();
            return Generation;
        }

        public int AliveCount
        {
            get
            {
                int c = 0;
                foreach (var v in cells) if (v == Alive) c++;
                return c;
            }
        }

        /// <summary>Jaccard similarity of the living cells and the target's living cells: 1 when they are the same set (also when both are empty), 0 when disjoint.</summary>
        public float Compare(byte[] target)
        {
            if (target == null || target.Length == 0) return 0f;
            int inter = 0, total = 0;
            int n = Math.Min(target.Length, cells.Length);
            for (int i = 0; i < n; i++)
            {
                bool a = cells[i] == Alive, b = target[i] == Alive;
                if (a && b) inter++;
                if (a || b) total++;
            }
            for (int i = n; i < cells.Length; i++) if (cells[i] == Alive) total++;
            return total == 0 ? 1f : (float)inter / total;
        }

        public int CopyCells(byte[] dst)
        {
            int n = Math.Min(dst.Length, cells.Length);
            Array.Copy(cells, dst, n);
            return n;
        }

        public int CopyAges(ushort[] dst)
        {
            int n = Math.Min(dst.Length, ages.Length);
            Array.Copy(ages, dst, n);
            return n;
        }
    }
}
