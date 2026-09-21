using System;
using System.Collections.Generic;
using UnityEngine;

namespace PointOfOrigin
{
    /// <summary>The things worth a small fanfare. Unlocks live in PlayerPrefs beside the chapter progress.</summary>
    public static class Achievements
    {
        public struct Entry
        {
            public string id, title, text;
            public bool hidden;   // shown as ??? until unlocked
        }

        public static readonly Entry[] All =
        {
            new Entry { id = "first_light", title = "First Light", text = "Find the origin of the first bloom." },
            new Entry { id = "exact", title = "Exact, First Try", text = "Find an origin on the first attempt." },
            new Entry { id = "untouched", title = "Untouched", text = "Finish a chapter without losing a lantern." },
            new Entry { id = "quick", title = "Quick Bloom", text = "Find the first origin within a minute of waking." },
            new Entry { id = "quench", title = "Quench", text = "Put an ember out with growth." },
            new Entry { id = "corners", title = "Every Corner", text = "See every open cell of a chapter before finding its origin, without revealing." },
            new Entry { id = "patient", title = "Patient", text = "Rewind ten times in one chapter and still find the origin." },
            new Entry { id = "hard_way", title = "The Hard Way", text = "Die to overgrowth, the void, an ember, acid, spikes and fire. Once each will do." },
            new Entry { id = "unaided", title = "Unaided", text = "Find all ten origins without ever revealing one." },
            new Entry { id = "every_origin", title = "Every Origin", text = "Find all ten." },
            new Entry { id = "secret", title = "Where It Really Began", text = "Find what the road does not lead to.", hidden = true },
        };

        const string Prefix = "po.ach.";
        static readonly Queue<Entry> toasts = new Queue<Entry>();

        public static bool Has(string id) => PlayerPrefs.GetInt(Prefix + id, 0) != 0;

        public static int Count()
        {
            int n = 0;
            foreach (var e in All) if (Has(e.id)) n++;
            return n;
        }

        /// <summary>Unlock once; returns true the first time, and queues a toast.</summary>
        public static bool Unlock(string id)
        {
            if (Has(id)) return false;
            PlayerPrefs.SetInt(Prefix + id, 1);
            PlayerPrefs.Save();
            foreach (var e in All) if (e.id == id) { toasts.Enqueue(e); break; }
            return true;
        }

        public static bool TryDequeueToast(out Entry e)
        {
            if (toasts.Count > 0) { e = toasts.Dequeue(); return true; }
            e = default;
            return false;
        }

        public static void ResetAll()
        {
            foreach (var e in All) PlayerPrefs.DeleteKey(Prefix + e.id);
            foreach (var k in new[] { "po.reveals", "po.secret", "po.died.OVERGROWN", "po.died.THE VOID TOOK YOU", "po.died.THE EMBER TOOK YOU", "po.died.THE ACID TOOK YOU", "po.died.THE SPIKES TOOK YOU", "po.died.THE FIRE TOOK YOU" })
                PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();
        }

        static readonly string[] Deaths = { "OVERGROWN", "THE VOID TOOK YOU", "THE EMBER TOOK YOU", "THE ACID TOOK YOU", "THE SPIKES TOOK YOU", "THE FIRE TOOK YOU" };

        /// <summary>Record a death by its banner text; unlock The Hard Way once all six have happened.</summary>
        public static void Died(string why)
        {
            PlayerPrefs.SetInt("po.died." + why, 1);
            foreach (var d in Deaths) if (PlayerPrefs.GetInt("po.died." + d, 0) == 0) return;
            Unlock("hard_way");
        }
    }
}
