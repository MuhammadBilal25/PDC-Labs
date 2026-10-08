using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    static readonly object gate = new object();
    static bool dataReady = false;
    static int value = 0;

    static int blocked = 0;

    static void Worker()
    {
        lock (gate)
        {
            while (!dataReady)
            {
                blocked++;
                Monitor.Wait(gate);
            }

            Console.WriteLine($"Worker received value: {value}");
        }
    }

    static void Publish(int newValue)
    {
        lock (gate)
        {
            value = newValue;
            dataReady = true;

            Monitor.Pulse(gate);
        }
    }

    static void SpinWorker()
    {
        while (!dataReady)
        {
            Thread.SpinWait(100);
        }

        Console.WriteLine($"Worker received value: {value}");
    }

    static void Main(string[] args)
    {
        string scenario = args.Length > 0 ? args[0] : "cond";

        Stopwatch wall = Stopwatch.StartNew();
        Stopwatch cpu = Stopwatch.StartNew();

        Thread[] workers;

        if (scenario == "cond")
        {
            workers = new Thread[1];

            workers[0] = new Thread(Worker);
            workers[0].Start();

            Thread.Sleep(100);

            Publish(123);

            workers[0].Join();
        }
        else if (scenario == "cond" && args.Length > 1)
        {
            workers = new Thread[1];

            workers[0] = new Thread(Worker);
            workers[0].Start();

            Thread.Sleep(100);

            Publish(123);

            workers[0].Join();
        }
        else if (scenario == "cond")
        {
            workers = new Thread[1];

            workers[0] = new Thread(Worker);
            workers[0].Start();

            Thread.Sleep(100);

            Publish(123);

            workers[0].Join();
        }
        else if (scenario == "cond")
        {
            workers = new Thread[1];

            workers[0] = new Thread(Worker);
            workers[0].Start();

            Publish(123);

            workers[0].Join();
        }
        else if (scenario == "spin")
        {
            dataReady = false;

            Thread worker = new Thread(SpinWorker);
            worker.Start();

            Thread.Sleep(100);

            Publish(123);

            worker.Join();
        }
        else if (scenario == "cond" && args.Length > 1)
        {
            Worker();
        }
        else
        {
            Console.WriteLine("Use: cond or spin");
            return;
        }

        wall.Stop();
        cpu.Stop();

        Console.WriteLine($"Times blocked in wait: {blocked}");
        Console.WriteLine($"Value received: {value}");
        Console.WriteLine($"Wall time (ms): {wall.Elapsed.TotalMilliseconds:F2}");
        Console.WriteLine($"CPU time (ms): {cpu.Elapsed.TotalMilliseconds:F2}");
    }
}