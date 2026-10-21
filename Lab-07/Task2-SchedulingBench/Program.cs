using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

class Policy
{
    public string Name;
    public string Kind;   // static, dynamic, guided or library
    public int Chunk;
    public Policy(string name, string kind, int chunk)
    {
        Name = name; Kind = kind; Chunk = chunk;
    }
}

class Program
{
    const int N = 20_000;
    const int Reps = 3;
    static int degree = 4;
    static bool uniform = false;

    static readonly long[] result   = new long[N];
    static readonly long[] expected = new long[N];

    static int nextIndex;                       // shared claim counter
    static readonly object claimLock = new object();

    static long Work(int i)
    {
        int steps = uniform ? N / 2 : i;        // uneven: iteration i costs i steps
        long acc = i;
        for (int j = 0; j < steps; j++)
            acc = unchecked(acc * 31 + (j ^ i));
        return acc;
    }

    static void Process(int start, int end)
    {
        for (int i = start; i < end; i++)
            result[i] = Work(i);
    }

    static void Worker(Policy p, int id)
    {
        switch (p.Kind)
        {
            case "static" when p.Chunk == 0:    // one equal block per thread
            {
                int start = (int)((long)id * N / degree);
                int end   = (int)((long)(id + 1) * N / degree);
                Process(start, end);
                break;
            }
            case "static":                       // chunks dealt out round-robin
                for (int s = id * p.Chunk; s < N; s += degree * p.Chunk)
                    Process(s, Math.Min(s + p.Chunk, N));
                break;

            case "dynamic":
            {
                while (true)
                {
                    int s = Interlocked.Add(ref nextIndex, p.Chunk) - p.Chunk;
                    if (s >= N) break;
                    Process(s, Math.Min(s + p.Chunk, N));
                }
                break;
            }
            case "guided":
            {
                while (true)
                {
                    int s, size;
                    lock (claimLock)
                    {
                        s = nextIndex;
                        int remaining = N - s;
                        if (remaining <= 0) break;
                        size = Math.Max(p.Chunk, remaining / (2 * degree));
                        if (size > remaining) size = remaining;
                        nextIndex = s + size;
                    }
                    Process(s, s + size);
                }
                break;
            }
        }
    }

    static void RunPolicy(Policy p, out double wall, out double bmax, out double bmin)
    {
        Array.Clear(result, 0, N);
        nextIndex = 0;
        double[] busy = new double[degree];
        Stopwatch total = Stopwatch.StartNew();

        if (p.Kind == "library")
        {
            ParallelOptions options = new ParallelOptions { MaxDegreeOfParallelism = degree };
            Parallel.For(0, N, options, i => result[i] = Work(i));
            bmax = bmin = -1;                    // per-thread times not available
        }
        else
        {
            Thread[] threads = new Thread[degree];
            for (int t = 0; t < degree; t++)
            {
                int id = t;
                threads[t] = new Thread(() =>
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    Worker(p, id);
                    busy[id] = sw.Elapsed.TotalMilliseconds;
                });
                threads[t].Start();
            }
            foreach (Thread th in threads) th.Join();

            bmax = busy[0]; bmin = busy[0];
            foreach (double b in busy)
            {
                if (b > bmax) bmax = b;
                if (b < bmin) bmin = b;
            }
        }
        wall = total.Elapsed.TotalMilliseconds;
    }

    static void Main(string[] args)
    {
        foreach (string a in args)
        {
            if (a == "uniform") uniform = true;
            else degree = int.Parse(a);
        }

        Policy[] policies =
        {
            new Policy("static",       "static",  0),
            new Policy("static,1",     "static",  1),
            new Policy("static,64",    "static",  64),
            new Policy("dynamic,1",    "dynamic", 1),
            new Policy("dynamic,64",   "dynamic", 64),
            new Policy("guided,1",     "guided",  1),
            new Policy("guided,64",    "guided",  64),
            new Policy("Parallel.For", "library", 0)
        };

        Stopwatch s0 = Stopwatch.StartNew();
        for (int i = 0; i < N; i++) expected[i] = Work(i);
        s0.Stop();

        string kind = uniform ? "uniform" : "uneven";
        Console.WriteLine($"workload={kind} n={N} degree={degree} " +
                          $"serial={s0.Elapsed.TotalMilliseconds:F1} (all times in ms)");

        foreach (Policy p in policies)
        {
            double best = double.MaxValue, bmax = 0, bmin = 0;
            bool ok = true;
            for (int r = 0; r < Reps; r++)
            {
                RunPolicy(p, out double w, out double mx, out double mn);
                if (!result.AsSpan().SequenceEqual(expected)) ok = false;
                if (w < best) { best = w; bmax = mx; bmin = mn; }
            }
            double ratio = bmin > 0.001 ? bmax / bmin : 0.0;
            string busyText = bmax < 0
                ? "busy_max= n/a  busy_min= n/a  imbalance= n/a"
                : $"busy_max={bmax,7:F1} busy_min={bmin,7:F1} imbalance={ratio,5:F2}";
            string okText = ok ? "YES" : "NO";
            Console.WriteLine($"[{p.Name,-12}] wall={best,7:F1} {busyText} correct={okText}");
        }
    }
}