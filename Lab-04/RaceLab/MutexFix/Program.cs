using System;
using System.Threading;

class Program
{
    const int NumThreads = 4;
    const int IncrementsPerThread = 1_000_000;

    static long counter = 0;

    // Lock object used to protect the shared counter
    static readonly object counterLock = new object();

    static void Worker()
    {
        for (int i = 0; i < IncrementsPerThread; i++)
        {
            // Only the shared update is inside the lock
            lock (counterLock)
            {
                counter++;
            }
        }
    }

    static void Main()
    {
        Thread[] threads = new Thread[NumThreads];

        // Create and start threads
        for (int i = 0; i < NumThreads; i++)
        {
            threads[i] = new Thread(Worker);
            threads[i].Start();
        }

        // Wait for all threads to finish
        for (int i = 0; i < NumThreads; i++)
        {
            threads[i].Join();
        }

        long expected = (long)NumThreads * IncrementsPerThread;
        long lostUpdates = expected - counter;

        Console.WriteLine("Task 3 - MutexFix");
        Console.WriteLine("-----------------");
        Console.WriteLine($"Expected: {expected}");
        Console.WriteLine($"Actual: {counter}");
        Console.WriteLine($"Lost updates: {lostUpdates}");
        Console.WriteLine($"Logical cores: {Environment.ProcessorCount}");
    }
}