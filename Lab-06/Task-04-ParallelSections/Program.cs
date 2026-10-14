using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static long ActionA()
    {
        long total = 0;
        for (int i = 1; i <= 18_000_000; i++)
            total += i % 17;
        return total;
    }

    static long ActionB()
    {
        long total = 0;
        for (int i = 1; i <= 15_000_000; i++)
            total += i % 23;
        return total;
    }

    static long ActionC()
    {
        long total = 0;
        for (int i = 1; i <= 12_000_000; i++)
            total += i % 31;
        return total;
    }

    static (long value, long ms) Measure(Func<long> action)
    {
        Stopwatch sw = Stopwatch.StartNew();
        long value = action();
        sw.Stop();
        return (value, sw.ElapsedMilliseconds);
    }

    static void Main(string[] args)
    {
        int degree = args.Length > 0 && int.TryParse(args[0], out int parsed)
            ? parsed : Environment.ProcessorCount;

        var aSerial = Measure(ActionA);
        var bSerial = Measure(ActionB);
        var cSerial = Measure(ActionC);
        long serialTotal = aSerial.ms + bSerial.ms + cSerial.ms;

        long a = 0, b = 0, c = 0;
        int threadA = 0, threadB = 0, threadC = 0;
        Stopwatch wall = Stopwatch.StartNew();

        Parallel.Invoke(
            new ParallelOptions { MaxDegreeOfParallelism = degree },
            () => { threadA = Thread.CurrentThread.ManagedThreadId; a = ActionA(); },
            () => { threadB = Thread.CurrentThread.ManagedThreadId; b = ActionB(); },
            () => { threadC = Thread.CurrentThread.ManagedThreadId; c = ActionC(); }
        );

        wall.Stop();

        bool resultsMatch = a == aSerial.value && b == bSerial.value && c == cSerial.value;
        double speedup = wall.Elapsed.TotalMilliseconds > 0
            ? (aSerial.ms + bSerial.ms + cSerial.ms) / wall.Elapsed.TotalMilliseconds : 0;

        Console.WriteLine($"Maximum degree = {degree}");
        Console.WriteLine($"Serial times (ms): A={aSerial.ms}, B={bSerial.ms}, C={cSerial.ms}, total={serialTotal}");
        Console.WriteLine($"Wall time (ms) = {wall.ElapsedMilliseconds}");
        Console.WriteLine($"Speedup = {speedup:F2}");
        Console.WriteLine($"Thread of A / B / C = {threadA} / {threadB} / {threadC}");
        Console.WriteLine($"Results match = {(resultsMatch ? "YES" : "NO")}");
        Console.WriteLine($"Action results: A={a}, B={b}, C={c}");
    }
}
