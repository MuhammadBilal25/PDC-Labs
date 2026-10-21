using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    const long Iters = 50_000_000;
    const int CacheLine = 64;
    const int Stride = CacheLine / sizeof(long); // 8 longs = 64 bytes

    static long[] adjacent;      // slots 8 bytes apart
    static long[] padded;        // slots 64 bytes apart
    static long[] localResult;   // written once per thread

    static double Run(int variant, int threads)
    {
        Thread[] ts = new Thread[threads];
        Stopwatch sw = Stopwatch.StartNew();

        for (int t = 0; t < threads; t++)
        {
            int id = t;
            ts[t] = new Thread(() =>
            {
                if (variant == 0)
                {
                    // Adjacent: 8 bytes apart (false sharing)
                    for (long i = 0; i < Iters; i++)
                        adjacent[id]++;
                }
                else if (variant == 1)
                {
                    // Padded: 64 bytes apart (no false sharing)
                    int idx = (id + 1) * Stride;
                    for (long i = 0; i < Iters; i++)
                        padded[idx]++;
                }
                else
                {
                    // Local accumulate: private variable, write once
                    long local = 0;
                    for (long i = 0; i < Iters; i++)
                        local++;
                    localResult[id] = local;
                }
            });
            ts[t].Start();
        }

        foreach (Thread th in ts) th.Join();
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }

    static void Main(string[] args)
    {
        int threads = Math.Min(Environment.ProcessorCount, 8);
        if (args.Length > 0) threads = int.Parse(args[0]);

        adjacent    = new long[threads];
        padded      = new long[(threads + 1) * Stride];
        localResult = new long[threads];

        Console.WriteLine($"threads={threads} iterations={Iters} per thread");
        Console.WriteLine($"adjacent slots are {sizeof(long)} bytes apart; " +
                          $"padded slots are {Stride * sizeof(long)} bytes apart");

        string[] names = { "adjacent", "padded", "local-accumulate" };
        double[] ms = new double[3];

        // Run each variant twice, take the second run (first is warm-up)
        for (int v = 0; v < 3; v++)
        {
            Run(v, threads);            // warm-up
            ms[v] = Run(v, threads);    // actual
        }

        bool ok = true;
        for (int t = 0; t < threads; t++)
            if (adjacent[t] != Iters || padded[(t + 1) * Stride] != Iters ||
                localResult[t] != Iters)
                ok = false;

        for (int v = 0; v < 3; v++)
            Console.WriteLine($"[{names[v],-16}] time={ms[v],8:F2} ms " +
                              $"slowdown vs padded={ms[v] / ms[1],5:F2}x");

        Console.WriteLine($"counters correct={(ok ? "YES" : "NO")}");
    }
}