using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    const int ItemsPerProducer = 50_000;

    // Defaults; override with:
    // dotnet run -- <producers> <consumers> <capacity>
    static int numProducers = 4, numConsumers = 4, bufferSize = 8;

    static int totalItems;
    static int[] buffer;

    static int inPos = 0, outPos = 0; // shared state

    static SemaphoreSlim emptySlots;
    static SemaphoreSlim fullSlots;

    static readonly object gate = new object();

    static int[] seen;

    static void Producer(int id)
    {
        for (int k = 0; k < ItemsPerProducer; k++)
        {
            int item = id * ItemsPerProducer + k;

            // Wait for a free slot
            emptySlots.Wait();

            // Insert item inside the lock
            lock (gate)
            {
                buffer[inPos] = item;
                inPos = (inPos + 1) % bufferSize;
            }

            // Announce that one slot is now filled
            fullSlots.Release();
        }
    }

    static void Consumer()
    {
        for (int k = 0; k < totalItems / numConsumers; k++)
        {
            int item;

            // Wait for a filled slot
            fullSlots.Wait();

            // Remove the oldest item inside the lock
            lock (gate)
            {
                item = buffer[outPos];
                outPos = (outPos + 1) % bufferSize;
            }

            // Announce that one slot is now free
            emptySlots.Release();

            Interlocked.Increment(ref seen[item]);
        }
    }

    static void Main(string[] args)
    {
        if (args.Length >= 3)
        {
            numProducers = int.Parse(args[0]);
            numConsumers = int.Parse(args[1]);
            bufferSize = int.Parse(args[2]);
        }

        totalItems = numProducers * ItemsPerProducer;

        if (totalItems % numConsumers != 0)
        {
            Console.WriteLine(
                "totalItems must be divisible by the number of consumers"
            );
            return;
        }

        buffer = new int[bufferSize];

        seen = new int[totalItems];

        emptySlots = new SemaphoreSlim(bufferSize, bufferSize);

        fullSlots = new SemaphoreSlim(0, bufferSize);

        Thread[] prod = new Thread[numProducers];
        Thread[] cons = new Thread[numConsumers];

        Stopwatch sw = Stopwatch.StartNew();

        // Start producer threads
        for (int i = 0; i < numProducers; i++)
        {
            int id = i;

            prod[i] = new Thread(() => Producer(id));

            prod[i].Start();
        }

        // Start consumer threads
        for (int i = 0; i < numConsumers; i++)
        {
            cons[i] = new Thread(Consumer);

            cons[i].Start();
        }

        // Wait for all producers
        foreach (Thread t in prod)
        {
            t.Join();
        }

        // Wait for all consumers
        foreach (Thread t in cons)
        {
            t.Join();
        }

        sw.Stop();

        long lost = 0;
        long duplicated = 0;

        for (int i = 0; i < totalItems; i++)
        {
            if (seen[i] == 0)
            {
                lost++;
            }
            else if (seen[i] > 1)
            {
                duplicated += seen[i] - 1;
            }
        }

        Console.WriteLine(
            $"[semaphores] producers={numProducers} consumers={numConsumers} " +
            $"buffer={bufferSize} items={totalItems}"
        );

        Console.WriteLine(
            $"lost={lost} duplicated={duplicated} " +
            $"correct={(lost == 0 && duplicated == 0 ? "YES" : "NO")} " +
            $"time={sw.Elapsed.TotalMilliseconds:F2} ms"
        );
    }
}