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

    public static Level Make(int n, System.Random rng)
    {
        // Level 1 = hand-made tutorial (0 soda, 1 donut, 2 fries)
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

        bool hard = n % 5 == 0;
        int T = Mathf.Min(3 + (int)(n * .8f) + (hard ? 2 : 0), Types);
        int E = hard ? 2 : n < 4 ? 4 : 3;
        // always 6 shelves (3 rows x 2) so the shelving unit fills the screen; difficulty grows through hidden rows instead
        const int S = 6, F = S * 3;
        int L = Mathf.CeilToInt((3f * T + E) / F);

        // ponytail: random deal, not guaranteed solvable (shuffle/undo cover it); add a reverse-generator if playtests hit dead levels
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
            return new Level { Shelves = shelves, Time = Mathf.Round((25 + T * 7) * (hard ? .85f : 1f)), Hard = hard };
        }
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
