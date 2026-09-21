// Run inside the open Editor, from the repository root, with the game in Play mode:
//     unity command eval_file --project-path unity/PointOfOrigin tools/editor/verify_jumps.cs
// Reads nx-out/candidates.json (from tools/platform_check.py --candidates=nx-out/candidates.json) and writes nx-out/verdicts.json for --verdicts.
// Replay every candidate jump with the real physics and write a verdict per edge for platform_check.py --verdicts.
var gc = UnityEngine.Object.FindAnyObjectByType<PointOfOrigin.GameController>();
var t = gc.GetType();
var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
System.Reflection.MethodInfo M(string n) => t.GetMethod(n, F);
object G(string n) => t.GetField(n, F).GetValue(gc);
void S(string n, object v) => t.GetField(n, F).SetValue(gc, v);
var stepPlayer = M("StepPlayer");
string dir = System.IO.Path.GetFullPath("../../nx-out") + System.IO.Path.DirectorySeparatorChar;
var text = System.IO.File.ReadAllText(dir + "candidates.json");
var edges = new System.Collections.Generic.List<(int level, bool grown, int cx, int cy, int ax, int ay, int bx, int by)>();
foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, "\\{[^{}]*\\}"))
{
    var s = m.Value;
    int I(string key) { var mm = System.Text.RegularExpressions.Regex.Match(s, "\"" + key + "\":\\s*(-?\\d+)"); return int.Parse(mm.Groups[1].Value); }
    (int, int) P(string key) { var mm = System.Text.RegularExpressions.Regex.Match(s, "\"" + key + "\":\\s*\\[\\s*(-?\\d+)\\s*,\\s*(-?\\d+)\\s*\\]"); return mm.Success ? (int.Parse(mm.Groups[1].Value), int.Parse(mm.Groups[2].Value)) : (-1, -1); }
    var a = P("from"); var b = P("to"); var c = P("carve");
    edges.Add((I("level"), s.Contains("\"grown\": true"), c.Item1, c.Item2, a.Item1, a.Item2, b.Item1, b.Item2));
}
var sb = new System.Text.StringBuilder("[");
int currentLevel = -1; bool currentGrown = false; int currentCx = -2, currentCy = -2;
byte[] baseRock = null;
int okCount = 0, n = 0;
var phaseType = t.GetNestedType("Phase", F);
var phasePlay = System.Enum.Parse(phaseType, "Play");
var phaseResult = System.Enum.Parse(phaseType, "Result");
foreach (var e in edges)
{
    if (e.level != currentLevel || e.grown != currentGrown || e.cx != currentCx || e.cy != currentCy)
    {
        M("StartLevel").Invoke(gc, new object[] { e.level - 1 });
        if (G("phase").ToString() == "Story") M("Primary").Invoke(gc, null);
        S("allSeen", true);
        ((System.Collections.IList)G("embers")).Clear();
        if (e.grown)
        {
            // the final growth, or, when one origin is still to be planted, only the others grown
            S("carried", 9);
            var answer = (System.Collections.Generic.HashSet<UnityEngine.Vector2Int>)G("answer");
            foreach (var o in answer)
                if (!(e.cx >= 0 && o.x == e.cx && o.y == e.cy)) M("ToggleSeed").Invoke(gc, new object[] { o });
            M("Grow").Invoke(gc, null);
            M("Primary").Invoke(gc, null);
        }
        currentLevel = e.level; currentGrown = e.grown; currentCx = e.cx; currentCy = e.cy;
    }
    var lv = (PointOfOrigin.Level)G("level");
    float hdir = System.Math.Sign(e.bx - e.ax);
    bool ok = false;
    float[] preRuns = { 0f, 0.1f, 0.22f, 0.36f, 0.55f };
    float[] holds = { 0.05f, 0.14f, 0.3f, 0.6f };
    foreach (var pre in preRuns) { foreach (var hold in holds) { foreach (var brake in new[] { false, true }) {
        S("pPos", new UnityEngine.Vector2(e.ax + 0.5f, lv.h - 1 - e.ay));
        S("pVel", UnityEngine.Vector2.zero); S("grounded", true); S("coyote", 0.1f); S("jumpBuffer", 0f);
        S("phase", e.grown ? phaseResult : phasePlay);
        float dt = 1f / 60f, time = 0f; bool jumped = false;
        for (int i = 0; i < 150 && !ok; i++)
        {
            var pos = (UnityEngine.Vector2)G("pPos");
            float move = hdir;
            if (brake && hdir != 0 && System.Math.Abs(pos.x - (e.bx + 0.5f)) < 0.35f) move = 0f;
            if (!jumped && time >= pre) { S("jumpBuffer", 0.12f); jumped = true; }
            bool held = jumped && time < pre + hold;
            stepPlayer.Invoke(gc, new object[] { dt, move, held });
            time += dt;
            if (G("phase").ToString() == "Dead") { S("phase", e.grown ? phaseResult : phasePlay); break; }
            pos = (UnityEngine.Vector2)G("pPos");
            int fx = (int)System.Math.Floor(pos.x), fy = lv.h - 1 - (int)System.Math.Floor(pos.y + 0.01f);
            if ((bool)G("grounded") && fx == e.bx && fy == e.by && time > pre + 0.05f) ok = true;
        }
        if (ok) break; } if (ok) break; } if (ok) break; }
    if (ok) okCount++;
    if (n++ > 0) sb.Append(",");
    sb.Append($"{{\"level\":{e.level},\"grown\":{(e.grown ? "true" : "false")},\"carve\":{(e.cx >= 0 ? $"[{e.cx},{e.cy}]" : "null")},\"from\":[{e.ax},{e.ay}],\"to\":[{e.bx},{e.by}],\"ok\":{(ok ? "true" : "false")}}}");
}
sb.Append("]");
System.IO.File.WriteAllText(dir + "verdicts.json", sb.ToString());
var summary = $"{okCount} of {n} candidate jumps confirmed";
System.IO.File.WriteAllText(dir + "verdicts_done.txt", summary);
return summary;
