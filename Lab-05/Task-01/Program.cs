using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    // ==============================
    // BUFFER SETTINGS
    // ==============================
    const int BufferSize = 64;
    const int NumberOfItems = 100000;

    // Shared circular buffer
    static int[] buffer = new int[BufferSize];

    // Circular buffer positions
    static int inPos = 0;
    static int outPos = 0;

    // Number of items currently in buffer
    static int count = 0;

    // Lock object
    static readonly object gate = new object();

    // Statistics
    static long producerRetries = 0;
    static long consumerRetries = 0;

    // Verification
    static long checksum = 0;
    static int orderErrors = 0;

    // ==============================
    // PRODUCER
    // ==============================
    static void Producer()
    {
        for (int item = 1; item <= NumberOfItems; item++)
        {
            bool inserted = false;

            while (!inserted)
            {
                lock (gate)
                {
                    // Check whether buffer has space
                    if (count < BufferSize)
                    {
                        // Insert item
                        buffer[inPos] = item;

                        // Move insertion position
                        inPos = (inPos + 1) % BufferSize;

                        // Increase number of items
                        count++;

                        inserted = true;
                    }
                }

                // Buffer was full
                if (!inserted)
                {
                    producerRetries++;

                    // Give another thread a chance to run
                    Thread.Yield();
                }
            }
        }
    }

    // ==============================
    // CONSUMER
    // ==============================
    static void Consumer()
    {
        int expected = 1;

        while (expected <= NumberOfItems)
        {
            bool removed = false;

            while (!removed)
            {
                int item = 0;

                lock (gate)
                {
                    // Check whether buffer has an item
                    if (count > 0)
                    {
                        // Remove item
                        item = buffer[outPos];

                        // Move removal position
                        outPos = (outPos + 1) % BufferSize;

                        // Decrease number of items
                        count--;

                        removed = true;
                    }
                }

                // Buffer was empty
                if (!removed)
                {
                    consumerRetries++;

                    // Give producer a chance to run
                    Thread.Yield();
                }
                else
                {
                    // ==============================
                    // VERIFY ORDER
                    // ==============================
                    if (item != expected)
                    {
                        orderErrors++;
                    }

                    // Add item to checksum
                    checksum += item;

                    expected++;
                }
            }
        }
    }

    // ==============================
    // MAIN
    // ==============================
    static void Main()
    {
        Console.WriteLine("======================================");
        Console.WriteLine(" PDC LAB 05 - TASK 1");
        Console.WriteLine(" Mutex-Protected Bounded Buffer");
        Console.WriteLine("======================================");

        Console.WriteLine($"Buffer Size   : {BufferSize}");
        Console.WriteLine($"Number Items  : {NumberOfItems}");
        Console.WriteLine();

        Stopwatch stopwatch = Stopwatch.StartNew();

        // Create producer and consumer threads
        Thread producerThread = new Thread(Producer);
        Thread consumerThread = new Thread(Consumer);

        // Start both threads
        producerThread.Start();
        consumerThread.Start();

        // Wait for both threads to finish
        producerThread.Join();
        consumerThread.Join();

        stopwatch.Stop();

        // Expected checksum:
        // 1 + 2 + 3 + ... + N
        long expectedChecksum =
            (long)NumberOfItems * (NumberOfItems + 1) / 2;

        bool checksumOK = checksum == expectedChecksum;
        bool allOK = checksumOK && orderErrors == 0;

        Console.WriteLine("--------------------------------------");
        Console.WriteLine("RESULTS");
        Console.WriteLine("--------------------------------------");

        Console.WriteLine($"Items             : {NumberOfItems}");
        Console.WriteLine($"Checksum          : {checksum}");
        Console.WriteLine($"Expected Checksum : {expectedChecksum}");
        Console.WriteLine($"Checksum OK       : {(checksumOK ? "Y" : "N")}");
        Console.WriteLine($"Order Errors      : {orderErrors}");
        Console.WriteLine($"Producer Retries  : {producerRetries}");
        Console.WriteLine($"Consumer Retries  : {consumerRetries}");
        Console.WriteLine($"Time (ms)         : {stopwatch.ElapsedMilliseconds}");
        Console.WriteLine($"Overall OK        : {(allOK ? "Y" : "N")}");
        Console.WriteLine("--------------------------------------");
    }
}
