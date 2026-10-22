using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MPI;

namespace SumCompare
{
    class Program
    {
        const int N = 10_000_000;
        const int Repetitions = 3;

        static void Main(string[] args)
        {
            using (new MPI.Environment(ref args))
            {
                Intracommunicator comm = Communicator.world;
                int rank = comm.Rank;
                int size = comm.Size;

                if (N % size != 0)
                {
                    if (rank == 0)
                    {
                        Console.WriteLine($"Error: Array size {N} must be divisible by {size}.");
                    }
                    return;
                }

                int chunkSize = N / size;
                int[] fullData = null;
                long expectedSum = (long)N;

                if (rank == 0)
                {
                    fullData = new int[N];
                    for (int i = 0; i < N; i++)
                    {
                        fullData[i] = 1;
                    }
                }

                // Prepare chunks for Scatter and P2P
                int[][] chunks = null;
                if (rank == 0)
                {
                    chunks = new int[size][];
                    for (int r = 0; r < size; r++)
                    {
                        chunks[r] = new int[chunkSize];
                        Array.Copy(fullData, r * chunkSize, chunks[r], 0, chunkSize);
                    }
                }

                // ==========================================
                // 1. MPI Collective Variant (Scatter & Reduce)
                // ==========================================
                double[] collectiveTimes = new double[Repetitions];
                bool collectiveCorrect = true;

                for (int rep = 0; rep < Repetitions; rep++)
                {
                    comm.Barrier();
                    Stopwatch sw = Stopwatch.StartNew();

                    // Scatter slices across ranks
                    int[] localData = comm.Scatter(chunks, 0);

                    long localSum = 0;
                    for (int i = 0; i < localData.Length; i++)
                    {
                        localSum += localData[i];
                    }

                    long globalSum = comm.Reduce(localSum, Operation<long>.Add, 0);

                    comm.Barrier();
                    sw.Stop();

                    if (rank == 0)
                    {
                        collectiveTimes[rep] = sw.Elapsed.TotalMilliseconds;
                        if (globalSum != expectedSum)
                            collectiveCorrect = false;
                    }
                }

                // ==========================================
                // 2. MPI Point-to-Point Variant (Send & Receive)
                // ==========================================
                double[] p2pTimes = new double[Repetitions];
                bool p2pCorrect = true;

                for (int rep = 0; rep < Repetitions; rep++)
                {
                    comm.Barrier();
                    Stopwatch sw = Stopwatch.StartNew();

                    int[] localData = new int[chunkSize];

                    if (rank == 0)
                    {
                        // Send chunk to each worker rank
                        for (int dest = 1; dest < size; dest++)
                        {
                            comm.Send(chunks[dest], dest, 100);
                        }
                        // Rank 0 keeps its own chunk
                        Array.Copy(chunks[0], 0, localData, 0, chunkSize);
                    }
                    else
                    {
                        // Worker receives pre-allocated array of chunkSize elements
                        comm.Receive(0, 100, ref localData);
                    }

                    long localSum = 0;
                    for (int i = 0; i < localData.Length; i++)
                    {
                        localSum += localData[i];
                    }

                    long globalSum = 0;
                    if (rank == 0)
                    {
                        globalSum = localSum;
                        for (int src = 1; src < size; src++)
                        {
                            long workerSum;
                            comm.Receive(src, 200, out workerSum);
                            globalSum += workerSum;
                        }
                    }
                    else
                    {
                        comm.Send(localSum, 0, 200);
                    }

                    comm.Barrier();
                    sw.Stop();

                    if (rank == 0)
                    {
                        p2pTimes[rep] = sw.Elapsed.TotalMilliseconds;
                        if (globalSum != expectedSum)
                            p2pCorrect = false;
                    }
                }

                // ==========================================
                // 3. Shared Memory Variant (TPL Parallel.For)
                // ==========================================
                double[] sharedTimes = new double[Repetitions];
                bool sharedCorrect = true;

                if (rank == 0)
                {
                    for (int rep = 0; rep < Repetitions; rep++)
                    {
                        Stopwatch sw = Stopwatch.StartNew();
                        long globalSum = 0;

                        Parallel.For(
                            0,
                            N,
                            new ParallelOptions { MaxDegreeOfParallelism = size },
                            () => 0L,
                            (i, loopState, subtotal) => subtotal + fullData[i],
                            subtotal => Interlocked.Add(ref globalSum, subtotal)
                        );

                        sw.Stop();
                        sharedTimes[rep] = sw.Elapsed.TotalMilliseconds;
                        if (globalSum != expectedSum)
                            sharedCorrect = false;
                    }
                }

                // ==========================================
                // Summary Reporting on Rank 0
                // ==========================================
                if (rank == 0)
                {
                    double avgCollective = (collectiveTimes[0] + collectiveTimes[1] + collectiveTimes[2]) / 3.0;
                    double avgP2p = (p2pTimes[0] + p2pTimes[1] + p2pTimes[2]) / 3.0;
                    double avgShared = (sharedTimes[0] + sharedTimes[1] + sharedTimes[2]) / 3.0;

                    bool allCorrect = collectiveCorrect && p2pCorrect && sharedCorrect;

                    Console.WriteLine($"=== Results for Ranks / Threads = {size} ===");
                    Console.WriteLine($"Problem size: {N:N0} elements | Expected sum: {expectedSum:N0}");
                    Console.WriteLine($"MPI Collective : Reps = [{collectiveTimes[0]:F2}, {collectiveTimes[1]:F2}, {collectiveTimes[2]:F2}] ms | Avg = {avgCollective:F2} ms | Correct: {(collectiveCorrect ? "YES" : "NO")}");
                    Console.WriteLine($"MPI Point-to-Pt: Reps = [{p2pTimes[0]:F2}, {p2pTimes[1]:F2}, {p2pTimes[2]:F2}] ms | Avg = {avgP2p:F2} ms | Correct: {(p2pCorrect ? "YES" : "NO")}");
                    Console.WriteLine($"Shared Memory  : Reps = [{sharedTimes[0]:F2}, {sharedTimes[1]:F2}, {sharedTimes[2]:F2}] ms | Avg = {avgShared:F2} ms | Correct: {(sharedCorrect ? "YES" : "NO")}");
                    Console.WriteLine($"Summary Table Line: {size}\t{avgCollective:F2}\t{avgP2p:F2}\t{avgShared:F2}\t{(allCorrect ? "YES" : "NO")}");
                }
            }
        }
    }
}
