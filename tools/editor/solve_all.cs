// Run inside the open Editor, from the repository root, with the game in Play mode:
//     unity command eval_file --project-path unity/PointOfOrigin tools/editor/solve_all.cs
// Solve every chapter with the designer's origins from a safe cell and report the match, plus what happened to the embers.
var gc = UnityEngine.Object.FindAnyObjectByType<PointOfOrigin.GameController>();
if (gc == null) return "no GameController in scene";
var t = gc.GetType();
var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
System.Reflection.MethodInfo M(string n) => t.GetMethod(n, F);
object G(string n) => t.GetField(n, F).GetValue(gc);
void S(string n, object v) => t.GetField(n, F).SetValue(gc, v);
var set = (PointOfOrigin.LevelSet)G("set");
var sb = new System.Text.StringBuilder();
M("StartLevel").Invoke(gc, new object[] { 0 });
for (int i = 0; i < set.levels.Length; i++)
{
    if (G("phase").ToString() == "Story") M("Primary").Invoke(gc, null);
    M("Rewind").Invoke(gc, null);
    var lv = set.levels[i];
    var seeds = (System.Collections.Generic.List<UnityEngine.Vector2Int>)G("seeds");
    foreach (var s in new System.Collections.Generic.List<UnityEngine.Vector2Int>(seeds))
        M("ToggleSeed").Invoke(gc, new object[] { s });
    S("carried", 9);
    var target = lv.TargetCells(); var rock = lv.RockCells();
    var emberSpecs = lv.EmberSpecs();
    UnityEngine.Vector2Int safe = new UnityEngine.Vector2Int(-1, -1);
    for (int y = lv.h - 1; y >= 0 && safe.x < 0; y--)
        for (int x = 0; x < lv.w && safe.x < 0; x++)
        {
            int idx = y * lv.w + x;
            if (target[idx] != 0 || rock[idx] != 0) continue;
            if (y + 1 < lv.h && rock[(y + 1) * lv.w + x] == 0) continue;   // want solid below
            bool near = false;
            for (int dx = -2; dx <= 2 && !near; dx++) for (int dy = -2; dy <= 2 && !near; dy++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= lv.w || ny >= lv.h) continue;
                if (target[ny * lv.w + nx] != 0) near = true;
            }
            bool onPatrol = false;
            foreach (var e in emberSpecs) if (e.vertical ? e.cell.x == x : e.cell.y == y) onPatrol = true;
            if (!near && !onPatrol) safe = new UnityEngine.Vector2Int(x, y);
        }
    S("pPos", new UnityEngine.Vector2(safe.x + 0.5f, lv.h - 1 - safe.y));
    S("pVel", UnityEngine.Vector2.zero);
    var answer = (System.Collections.Generic.HashSet<UnityEngine.Vector2Int>)G("answer");
    foreach (var a in answer) M("ToggleSeed").Invoke(gc, new object[] { a });
    int embersBefore = ((System.Collections.ICollection)G("embers")).Count;
    M("Grow").Invoke(gc, null);
    M("Primary").Invoke(gc, null);
    int embersAfter = ((System.Collections.ICollection)G("embers")).Count;
    sb.Append($"L{i + 1} {lv.name}: phase={G("phase")} won={G("won")} match={(float)G("match"):0.000} safe={safe} embers {embersBefore}->{embersAfter}\n");
    if (i + 1 < set.levels.Length) M("Next").Invoke(gc, null);
}
sb.Append($"unlocked={G("unlocked")} solvedMask={G("solvedMask")} lives={G("lives")}");
return sb.ToString();
