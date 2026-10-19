// Generates levels 2..201 (level 1 is the hand-made tutorial), re-checks each one after a round trip
// through the text format, and writes them to Assets/Resources/levels.txt.
using System.Diagnostics;

int Count = args.Length > 0 ? int.Parse(args[0]) : 200; // pass a smaller count to preview (still writes the file)
var rng = new System.Random(2026);
var sw = Stopwatch.StartNew();
var lines = new List<string>();
for (int n = 2; n < 2 + Count; n++)
{
    var line = LevelGen.Encode(LevelGen.Generate(n, rng));
    var back = LevelGen.Decode(line);
    if (back.Shelves.SelectMany(s => s).SelectMany(r => r).Where(x => x >= 0).GroupBy(LevelGen.Id).Any(g => g.Count() != 3))
        throw new Exception($"Level {n}: bad item counts");
    if (!LevelGen.Solvable(back.Shelves)) throw new Exception($"Level {n}: not solvable after decode");
    lines.Add(line);
}
var path = Path.GetFullPath(Path.Combine("..", "..", "Assets", "Resources", "levels.txt"));
File.WriteAllText(path, string.Join("\n", lines) + "\n");
Console.WriteLine($"Baked {Count} levels in {sw.ElapsedMilliseconds} ms -> {path}");
foreach (int n in new[] { 2, 7, 8, 9, 20, 40, 80 }.Where(n => n - 2 < lines.Count))
{
    var lv = LevelGen.Decode(lines[n - 2]);
    int ice = lv.Shelves.SelectMany(s => s).SelectMany(r => r).Count(LevelGen.IsFrozen);
    Console.WriteLine($"  level {n}: par {lv.Par}, {ice} frozen, {lv.Time}s");
}
