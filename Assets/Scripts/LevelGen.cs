using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// A shelf is a list of rows of 3 slots, row 0 being the playable front and the rest waiting behind it.
// A slot holds -1 (empty), a product id 0..11, or:
//  - id + Frozen: stuck in ice, can't be picked up, only leaves when two matching products are brought next to it;
//  - id + Boxed: hidden in a "?" carton, opened when picked up (the solver knows what's inside, the player doesn't);
//  - Crate: a wooden crate taking a front slot, broken by a trio cleared on a neighboring shelf.
public static class LevelGen
{
    public const int Types = 12, Crate = 99, Frozen = 100, Boxed = 200;

    public static int Id(int x) => x % 100;
    public static bool IsFrozen(int x) => x >= Frozen && x < Boxed;
    public static bool IsBoxed(int x) => x >= Boxed;
    public static bool IsProduct(int x) => x >= 0 && x != Crate;
    public static bool Movable(int x) => IsProduct(x) && !IsFrozen(x);

    // shelves next to s in the 2-column unit: the other column, the row above, the row below
    public static IEnumerable<int> Neighbors(int s, int count) => new[] { s ^ 1, s - 2, s + 2 }.Where(n => n >= 0 && n < count);

    public class Level
    {
        public List<List<int[]>> Shelves;
        public float Time;
        public bool Hard;
        public int Par; // move target for the "moves" star
    }

    public static bool Triple(int[] r) => r[0] >= 0 && r[1] >= 0 && r[2] >= 0 && Id(r[0]) == Id(r[1]) && Id(r[1]) == Id(r[2]);

    // Levels are generated + solved ahead of time (Tools/LevelBaker writes Resources/levels.txt) because finding
    // a solvable deal can take seconds on a phone. At runtime we only read a line and relabel the products.
    public static Level Make(int n, System.Random rng)
    {
        // Level 1 = hand-made tutorial, solved in 5 moves
        if (n == 1)
            return new Level
            {
                Time = 60,
                Par = 6,
                Shelves = new List<List<int[]>>
                {
                    new() { new[] { 0, 1, 0 } },
                    new() { new[] { 2, 0, 1 } },
                    new() { new[] { 1, 2, -1 } },
                    new() { new[] { 2, -1, -1 } },
                }
            };

        var lines = Baked();
        int i = n - 2;
        if (i >= lines.Length) i = lines.Length - 50 + (i - lines.Length) % 50; // past the end: loop the last 50
        var lv = Decode(lines[i]);
        // swapping which product is which keeps the level solvable, and makes replays look different
        var perm = Shuffle(Enumerable.Range(0, Types).ToList(), rng);
        foreach (var sh in lv.Shelves)
            foreach (var r in sh)
                for (int k = 0; k < 3; k++)
                    if (IsProduct(r[k])) r[k] = perm[Id(r[k])] + (r[k] - Id(r[k])); // keeps the ice/box flag
        return lv;
    }

    static string[] baked;
    static string[] Baked() =>
        baked ??= Resources.Load<TextAsset>("levels").text.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);

    // One level per line: "time;hard;par;shelf|shelf|..." where a shelf is rows joined by '/', one char per slot:
    // 'a' empty, 'b'..'m' a product, 'B'..'M' the same product frozen, 'n'..'y' boxed, 'z' a crate
    static char Char(int x) => x < 0 ? 'a' : x == Crate ? 'z' : (char)((IsFrozen(x) ? 'B' : IsBoxed(x) ? 'n' : 'b') + Id(x));
    static int FromChar(char c) => c == 'a' ? -1 : c == 'z' ? Crate : char.IsUpper(c) ? c - 'B' + Frozen : c >= 'n' ? c - 'n' + Boxed : c - 'b';

    public static string Encode(Level lv) =>
        $"{lv.Time.ToString(System.Globalization.CultureInfo.InvariantCulture)};{(lv.Hard ? 1 : 0)};{lv.Par};" +
        string.Join("|", lv.Shelves.Select(sh => string.Join("/", sh.Select(r => new string(r.Select(Char).ToArray())))));

    public static Level Decode(string line)
    {
        var p = line.Trim().Split(';');
        return new Level
        {
            Time = float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture),
            Hard = p[1] == "1",
            Par = int.Parse(p[2]),
            Shelves = p[3].Split('|').Select(sh => sh.Split('/').Select(r => r.Select(FromChar).ToArray()).ToList()).ToList(),
        };
    }

    // Expensive: random deals until the solver clears one. Only the baker calls this.
    public static Level Generate(int n, System.Random rng)
    {
        bool hard = n % 5 == 0;
        int T = Mathf.Min(3 + (int)(n * .8f) + (hard ? 2 : 0), Types);
        int E = hard ? 2 : n < 4 ? 4 : 3;
        // obstacles, one new kind per stretch of levels, then more of them
        int ice = n < 8 ? 0 : Mathf.Min(1 + (n - 8) / 8, 4); // frozen products from level 8
        int boxes = n < 14 ? 0 : Mathf.Min(1 + (n - 14) / 6, 5); // "?" cartons from level 14
        int crates = n < 24 ? 0 : Mathf.Min(1 + (n - 24) / 12, 3); // wooden crates from level 24
        // always 6 shelves (3 rows x 2) so the shelving unit fills the screen; difficulty grows through hidden rows instead
        const int S = 6, F = S * 3;
        int L = Mathf.CeilToInt((3f * T + E + crates) / F);

        // random deals, kept only when the solver finds a way through
        while (true)
        {
            var kinds = Shuffle(Enumerable.Range(0, Types).ToList(), rng).Take(T);
            var items = Shuffle(kinds.SelectMany(k => new[] { k, k, k }).ToList(), rng);
            int nf = Mathf.Min(items.Count, F - E - crates); // crates take front slots: their products wait behind
            var front = Chunk(Shuffle(items.Take(nf).Concat(Enumerable.Repeat(Crate, crates)).Concat(Enumerable.Repeat(-1, F - nf - crates)).ToList(), rng));
            if (front.Any(Triple) || front.Any(r => r.Count(x => x == Crate) > 1)) continue;

            // freeze some front products; a shelf with two different frozen products could never be completed
            var cells = Shuffle(Enumerable.Range(0, F).Where(c => IsProduct(front[c / 3][c % 3])).ToList(), rng);
            foreach (int c in cells.Take(ice)) front[c / 3][c % 3] += Frozen;
            if (front.Any(r => r.Where(IsFrozen).Select(Id).Distinct().Count() > 1)) continue;

            var rest = items.Skip(nf).ToList();
            var back = Chunk(Shuffle(rest.Concat(Enumerable.Repeat(-1, F * (L - 1) - rest.Count)).ToList(), rng));

            // hide some products in cartons, front or back
            var plain = Shuffle(front.Concat(back).SelectMany(r => Enumerable.Range(0, 3).Select(k => (r, k)))
                .Where(c => c.r[c.k] >= 0 && c.r[c.k] < Types).ToList(), rng);
            foreach (var (r, k) in plain.Take(boxes)) r[k] += Boxed;

            var shelves = new List<List<int[]>>();
            for (int i = 0; i < S; i++)
            {
                var shelf = new List<int[]> { front[i] };
                for (int k = 0; k < L - 1; k++) shelf.Add(back[k * S + i]);
                shelves.Add(shelf);
            }

            int dfs = Solve(shelves);
            if (dfs < 0) continue;
            // the DFS line is far from the shortest; a beam search gets close to it
            int best = Beam(shelves);
            int moves = best > 0 ? Mathf.Min(best, dfs) : dfs;
            return new Level
            {
                Shelves = shelves,
                Time = Mathf.Round((25 + T * 7) * (hard ? .85f : 1f)),
                Hard = hard,
                // a little slack: a careful player can make it, a hasty one can't; cartons hide what the solver saw
                Par = moves + 1 + moves / 8 + (boxes + 1) / 2,
            };
        }
    }

    // ---------- rules, shared with the game ----------

    public static List<List<int[]>> Clone(List<List<int[]>> b) => b.Select(sh => sh.Select(r => (int[])r.Clone()).ToList()).ToList();

    public static bool Cleared(List<List<int[]>> b) => b.All(sh => sh.All(r => r.All(x => x < 0)));

    // Same order as the game: bring hidden rows forward, clear trios (breaking crates next door), repeat until stable
    static void Settle(List<List<int[]>> b)
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var sh in b)
                while (sh.Count > 1 && sh[0].All(x => x < 0)) { sh.RemoveAt(0); changed = true; }
            var hits = Enumerable.Range(0, b.Count).Where(s => Triple(b[s][0])).ToList();
            foreach (int s in hits) b[s][0] = new[] { -1, -1, -1 };
            BreakCrates(b, hits);
            changed |= hits.Count > 0;
        }
    }

    // crates on the front rows next to the cleared shelves break; returns the (shelf, slot) of each one
    public static List<(int s, int i)> BreakCrates(List<List<int[]>> b, List<int> cleared)
    {
        var broken = cleared.SelectMany(s => Neighbors(s, b.Count)).Distinct()
            .SelectMany(n => Enumerable.Range(0, 3).Where(i => b[n][0][i] == Crate).Select(i => (n, i))).ToList();
        foreach (var (s, i) in broken) b[s][0][i] = -1;
        return broken;
    }

    public static bool Solvable(List<List<int[]>> start) => Solve(start) >= 0;

    // Depth-first search, most promising moves first (trios, then pairs). Returns the number of moves of the
    // solution found, or -1. Solvable deals are found in a few dozen states, so giving up early (maxNodes) just
    // rejects the deal fast; it also rejects levels that only an obscure line solves.
    public static int Solve(List<List<int[]>> start, int maxNodes = 400, int maxDepth = 80)
    {
        var seen = new HashSet<string>();
        int nodes = 0;

        int Dfs(List<List<int[]>> b, int depth)
        {
            if (Cleared(b)) return depth;
            if (depth >= maxDepth || ++nodes > maxNodes || !seen.Add(Key(b))) return -1;

            var moves = new List<(int s, int i, int t, int j, int score)>();
            for (int t = 0; t < b.Count; t++)
            {
                int j = System.Array.IndexOf(b[t][0], -1);
                if (j < 0) continue; // one empty slot per target shelf is enough: they are equivalent
                for (int s = 0; s < b.Count; s++)
                {
                    if (s == t) continue;
                    for (int i = 0; i < 3; i++)
                    {
                        int x = b[s][0][i];
                        if (!Movable(x)) continue;
                        int same = b[t][0].Count(y => IsProduct(y) && Id(y) == Id(x));
                        moves.Add((s, i, t, j, same * same * 3 - b[s][0].Count(y => IsProduct(y) && Id(y) == Id(x))));
                    }
                }
            }
            foreach (var m in moves.OrderByDescending(m => m.score))
            {
                var nb = Clone(b);
                nb[m.t][0][m.j] = nb[m.s][0][m.i];
                nb[m.s][0][m.i] = -1;
                Settle(nb);
                int found = Dfs(nb, depth + 1);
                if (found >= 0) return found;
            }
            return -1;
        }

        var b0 = Clone(start);
        Settle(b0);
        return Dfs(b0, 0);
    }

    // Beam search for a short solution: expand every state of the current depth, keep the `width` most promising
    // (fewest products left, then most pairs already together). Returns the moves of the first clear, or -1.
    public static int Beam(List<List<int[]>> start, int width = 150, int maxDepth = 80)
    {
        var b0 = Clone(start);
        Settle(b0);
        var frontier = new List<List<List<int[]>>> { b0 };
        var seen = new HashSet<string> { Key(b0) };
        for (int depth = 1; depth <= maxDepth && frontier.Count > 0; depth++)
        {
            var next = new List<(List<List<int[]>> b, int h)>();
            foreach (var b in frontier)
                for (int t = 0; t < b.Count; t++)
                {
                    int j = System.Array.IndexOf(b[t][0], -1);
                    if (j < 0) continue;
                    for (int s = 0; s < b.Count; s++)
                    {
                        if (s == t) continue;
                        for (int i = 0; i < 3; i++)
                        {
                            int x = b[s][0][i];
                            if (!Movable(x)) continue;
                            var nb = Clone(b);
                            nb[t][0][j] = x;
                            nb[s][0][i] = -1;
                            Settle(nb);
                            if (Cleared(nb)) return depth;
                            if (seen.Add(Key(nb))) next.Add((nb, Promise(nb)));
                        }
                    }
                }
            frontier = next.OrderBy(n => n.h).Take(width).Select(n => n.b).ToList();
        }
        return -1;
    }

    // lower is better: products left on the board, minus a bit for every front row holding a matching pair
    static int Promise(List<List<int[]>> b)
    {
        int left = 0, pairs = 0;
        foreach (var sh in b)
        {
            foreach (var r in sh)
                foreach (int x in r)
                    if (x >= 0) left++;
            var f = sh[0].Where(x => x >= 0).Select(Id).ToList();
            if (f.Count >= 2 && f.Distinct().Count() < f.Count) pairs++;
        }
        return left * 3 - pairs * 2;
    }

    static readonly System.Text.StringBuilder keyBuf = new();

    static string Key(List<List<int[]>> b)
    {
        keyBuf.Clear();
        foreach (var sh in b)
        {
            foreach (var r in sh)
                foreach (int x in r) keyBuf.Append(Char(x));
            keyBuf.Append('|');
        }
        return keyBuf.ToString();
    }

    public static List<T> Shuffle<T>(List<T> a, System.Random rng)
    {
        for (int i = a.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }
        return a;
    }

    static List<int[]> Chunk(List<int> a)
    {
        var r = new List<int[]>();
        for (int i = 0; i < a.Count; i += 3) r.Add(new[] { a[i], a[i + 1], a[i + 2] });
        return r;
    }
}
