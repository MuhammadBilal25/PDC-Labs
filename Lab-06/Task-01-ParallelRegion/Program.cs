using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static void Main(string[] args)
    {
        int degree = args.Length > 0 && int.TryParse(args[0], out int parsed)
            ? parsed : Environment.ProcessorCount;

        if (degree < 1)
        {
            Console.WriteLine("Degree must be at least 1.");
            return;
        }

        Console.WriteLine($"Environment.ProcessorCount = {Environment.ProcessorCount}");
        Console.WriteLine($"Maximum degree of parallelism = {degree}");

        var threadIds = new ConcurrentDictionary<int, byte>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = degree };
        Stopwatch watch = Stopwatch.StartNew();

        Parallel.For(0, 16, options, i =>
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            threadIds.TryAdd(threadId, 0);
            Console.WriteLine($"Iteration {i}: thread ID {threadId}");
            Thread.Sleep(100);
        });

        watch.Stop();
        Console.WriteLine($"Distinct threads used = {threadIds.Count}");
        Console.WriteLine($"Thread IDs = {string.Join(", ", threadIds.Keys.OrderBy(x => x))}");
        Console.WriteLine($"Elapsed time (ms) = {watch.ElapsedMilliseconds}");
        Console.WriteLine($"Rough predicted time (ms) = {((16 + degree - 1) / degree) * 100}");
    }
}
