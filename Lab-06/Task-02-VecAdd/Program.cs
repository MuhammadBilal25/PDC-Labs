
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

class Program
{
    static void Main(string[] args)
    {
        int degree = args.Length > 0 && int.TryParse(args[0], out int parsed)
            ? parsed : Environment.ProcessorCount;

        const int N = 5_000_000;
        const int Passes = 20;

        int[] a = new int[N];
        int[] b = new int[N];
        int[] result = new int[N];

        for (int i = 0; i < N; i++)
        {
            a[i] = i;
            b[i] = i * 2;
        }

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = degree
        };

        Stopwatch watch = Stopwatch.StartNew();

        for (int pass = 0; pass < Passes; pass++)
        {
            Parallel.ForEach(
                Partitioner.Create(0, N),
                options,
                range =>
                {
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        result[i] = a[i] + b[i];
                    }
                });
        }

        watch.Stop();

        bool correct = true;

        for (int i = 0; i < N; i++)
        {
            if (result[i] != a[i] + b[i])
            {
                correct = false;
                break;
            }
        }

        Console.WriteLine($"N = {N}, passes = {Passes}, degree = {degree}");
        Console.WriteLine($"Elapsed time (ms) = {watch.ElapsedMilliseconds}");
        Console.WriteLine($"Correct = {(correct ? "YES" : "NO")}");
    }
}
