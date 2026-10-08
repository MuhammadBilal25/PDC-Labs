using System;
using System.Diagnostics;
using System.Threading;

class PhaseBarrier
{
    private readonly object gate = new object();

    private int parties;
    private int waiting;
    private int generation;

    public PhaseBarrier(int parties)
    {
        this.parties = parties;
        waiting = 0;
        generation = 0;
    }

    public void SignalAndWait()
    {
        lock (gate)
        {
            int myGeneration = generation;

            waiting++;

            if (waiting == parties)
            {
                waiting = 0;
                generation++;

                Monitor.PulseAll(gate);
            }
            else
            {
                while (myGeneration == generation)
                {
                    Monitor.Wait(gate);
                }
            }
        }
    }
}

class Program
{
    static int workers = 4;
    static int rounds = 5;

    static int[] results = new int[4];

    static PhaseBarrier barrier = new PhaseBarrier(workers);

    static void Worker(int id)
    {
        for (int round = 0; round < rounds; round++)
        {
            Thread.Sleep(10 + id * 2);

            results[id]++;

            barrier.SignalAndWait();

            int total = 0;

            for (int i = 0; i < workers; i++)
            {
                total += results[i];
            }

            if (total != (round + 1) * workers)
            {
                Console.WriteLine("Mismatch detected.");
            }

            barrier.SignalAndWait();
        }
    }

    static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            workers = int.Parse(args[0]);
        }

        barrier = new PhaseBarrier(workers);
        results = new int[workers];

        Stopwatch sw = Stopwatch.StartNew();

        Thread[] threads = new Thread[workers];

        for (int i = 0; i < workers; i++)
        {
            int id = i;

            threads[i] = new Thread(() => Worker(id));
            threads[i].Start();
        }

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        sw.Stop();

        bool correct = true;

        for (int i = 0; i < workers; i++)
        {
            if (results[i] != rounds)
            {
                correct = false;
            }
        }

        Console.WriteLine($"Workers: {workers}");
        Console.WriteLine($"Correct: {(correct ? "Y" : "N")}");
        Console.WriteLine($"Time (ms): {sw.Elapsed.TotalMilliseconds:F2}");
        Console.WriteLine($"Environment.ProcessorCount: {Environment.ProcessorCount}");
    }
}