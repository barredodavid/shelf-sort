using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// A shelf is a list of rows of 3 item ids (-1 = empty). Row 0 is the playable front, the rest wait behind it.
public static class LevelGen
{
    public const int Types = 12;

    public class Level
    {
        public List<List<int[]>> Shelves;
        public float Time;
        public bool Hard;
    }

    public static bool Triple(int[] r) => r[0] >= 0 && r[0] == r[1] && r[1] == r[2];

    // Levels are generated + solved ahead of time (Tools/LevelBaker writes Resources/levels.txt) because finding
    // a solvable deal can take seconds on a phone. At runtime we only read a line and relabel the products.
    public static Level Make(int n, System.Random rng)
    {
        // Level 1 = hand-made tutorial
        if (n == 1)
            return new Level
            {
                Time = 60,
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
                    if (r[k] >= 0) r[k] = perm[r[k]];
        return lv;
    }

    static string[] baked;
    static string[] Baked() =>
        baked ??= Resources.Load<TextAsset>("levels").text.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);

    // One level per line: "time;hard;shelf|shelf|..." where a shelf is rows joined by '/', one char per slot ('a' = empty)
    public static string Encode(Level lv) =>
        $"{lv.Time.ToString(System.Globalization.CultureInfo.InvariantCulture)};{(lv.Hard ? 1 : 0)};" +
        string.Join("|", lv.Shelves.Select(sh => string.Join("/", sh.Select(r => new string(r.Select(x => (char)('a' + x + 1)).ToArray())))));

    public static Level Decode(string line)
    {
        var p = line.Trim().Split(';');
        return new Level
        {
            Time = float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture),
            Hard = p[1] == "1",
            Shelves = p[2].Split('|').Select(sh => sh.Split('/').Select(r => r.Select(c => c - 'a' - 1).ToArray()).ToList()).ToList(),
        };
    }

    // Expensive: random deals until the solver clears one. Only the baker calls this.
    public static Level Generate(int n, System.Random rng)
    {
        bool hard = n % 5 == 0;
        int T = Mathf.Min(3 + (int)(n * .8f) + (hard ? 2 : 0), Types);
        int E = hard ? 2 : n < 4 ? 4 : 3;
        // always 6 shelves (3 rows x 2) so the shelving unit fills the screen; difficulty grows through hidden rows instead
        const int S = 6, F = S * 3;
        int L = Mathf.CeilToInt((3f * T + E) / F);

        // random deals, kept only when the solver finds a way through
        while (true)
        {
            var kinds = Shuffle(Enumerable.Range(0, Types).ToList(), rng).Take(T);
            var items = Shuffle(kinds.SelectMany(k => new[] { k, k, k }).ToList(), rng);
            int nf = Mathf.Min(items.Count, F - E);
            var front = Chunk(Shuffle(items.Take(nf).Concat(Enumerable.Repeat(-1, F - nf)).ToList(), rng));
            if (front.Any(Triple)) continue;

            var rest = items.Skip(nf).ToList();
            var back = Chunk(Shuffle(rest.Concat(Enumerable.Repeat(-1, F * (L - 1) - rest.Count)).ToList(), rng));

            var shelves = new List<List<int[]>>();
            for (int i = 0; i < S; i++)
            {
                var shelf = new List<int[]> { front[i] };
                for (int k = 0; k < L - 1; k++) shelf.Add(back[k * S + i]);
                shelves.Add(shelf);
            }
            if (!Solvable(shelves)) continue;
            return new Level { Shelves = shelves, Time = Mathf.Round((25 + T * 7) * (hard ? .85f : 1f)), Hard = hard };
        }
    }

    // ---------- rules, shared with the game ----------

    public static List<List<int[]>> Clone(List<List<int[]>> b) => b.Select(sh => sh.Select(r => (int[])r.Clone()).ToList()).ToList();

    public static bool Cleared(List<List<int[]>> b) => b.All(sh => sh.All(r => r.All(x => x < 0)));

    // Same order as the game: bring hidden rows forward, clear trios, repeat until nothing changes
    static void Settle(List<List<int[]>> b)
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var sh in b)
            {
                while (sh.Count > 1 && sh[0].All(x => x < 0)) { sh.RemoveAt(0); changed = true; }
                if (Triple(sh[0])) { sh[0] = new[] { -1, -1, -1 }; changed = true; }
            }
        }
    }

    // Depth-first search, most promising moves first (trios, then pairs). Solvable deals are found in a few
    // dozen states, so giving up early (maxNodes) just rejects the deal fast; it also rejects levels that only
    // an obscure line solves, which a casual player wouldn't find anyway.
    public static bool Solvable(List<List<int[]>> start, int maxNodes = 400, int maxDepth = 80)
    {
        var seen = new HashSet<string>();
        int nodes = 0;

        bool Dfs(List<List<int[]>> b, int depth)
        {
            if (Cleared(b)) return true;
            if (depth > maxDepth || ++nodes > maxNodes || !seen.Add(Key(b))) return false;

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
                        int id = b[s][0][i];
                        if (id < 0) continue;
                        int same = b[t][0].Count(x => x == id);
                        moves.Add((s, i, t, j, same * same * 3 - b[s][0].Count(x => x == id)));
                    }
                }
            }
            foreach (var m in moves.OrderByDescending(m => m.score))
            {
                var nb = Clone(b);
                nb[m.t][0][m.j] = nb[m.s][0][m.i];
                nb[m.s][0][m.i] = -1;
                Settle(nb);
                if (Dfs(nb, depth + 1)) return true;
            }
            return false;
        }

        var b0 = Clone(start);
        Settle(b0);
        return Dfs(b0, 0);
    }

    static readonly System.Text.StringBuilder keyBuf = new();

    static string Key(List<List<int[]>> b)
    {
        keyBuf.Clear();
        foreach (var sh in b)
        {
            foreach (var r in sh)
                foreach (int x in r) keyBuf.Append((char)('a' + x + 1)); // one char per slot ('a' = empty)
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
