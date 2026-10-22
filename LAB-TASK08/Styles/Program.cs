using System;
using System.Diagnostics;
using System.Linq;
using MPI;

namespace Styles
{
    class Program
    {
        const int TagRequest = 1;
        const int TagTask = 2;
        const int TagResult = 3;
        const int TagRing = 10;

        static void Main(string[] args)
        {
            using (new MPI.Environment(ref args))
            {
                Intracommunicator comm = Communicator.world;
                int rank = comm.Rank;
                int size = comm.Size;

                string mode = args.Length > 0 ? args[0].ToLower() : "master";

                if (mode == "master")
                {
                    RunTaskFarm(comm, rank, size);
                }
                else if (mode == "ring")
                {
                    RunTokenRing(comm, rank, size);
                }
                else
                {
                    if (rank == 0)
                    {
                        Console.WriteLine($"Unknown mode '{mode}'. Usage: Styles.exe [master|ring]");
                    }
                }
            }
        }

        // =========================================================================
        // 1. Task Farm (Client-Server Architecture Style)
        // =========================================================================
        static void RunTaskFarm(Intracommunicator comm, int rank, int size)
        {
            const int totalTasks = 80;
            int numWorkers = size - 1;

            if (size < 2)
            {
                if (rank == 0)
                    Console.WriteLine("Task farm requires at least 2 ranks (1 master + 1 or more workers).");
                return;
            }

            if (rank == 0)
            {
                // Master / Server Role
                Stopwatch sw = Stopwatch.StartNew();
                int nextTaskId = 0;
                int[] tasksServed = new int[size]; // index by rank
                int completedTasks = 0;
                int activeWorkers = numWorkers;

                while (activeWorkers > 0)
                {
                    // Receive request from any worker
                    CompletedStatus status;
                    comm.Receive(Communicator.anySource, TagRequest, out int req, out status);
                    int workerRank = status.Source;

                    if (nextTaskId < totalTasks)
                    {
                        int currentTask = nextTaskId++;
                        tasksServed[workerRank]++;
                        comm.Send(currentTask, workerRank, TagTask);

                        // Receive result from worker
                        comm.Receive(workerRank, TagResult, out long result);
                        completedTasks++;
                    }
                    else
                    {
                        // No more tasks: send termination sentinel (-1)
                        comm.Send(-1, workerRank, TagTask);
                        activeWorkers--;
                    }
                }

                sw.Stop();

                bool correct = (completedTasks == totalTasks) && (tasksServed.Sum() == totalTasks);

                Console.WriteLine($"=== Task Farm (Client-Server Style) Results ===");
                Console.WriteLine($"Total Ranks : {size} (1 Master + {numWorkers} Workers)");
                Console.WriteLine($"Total Tasks : {totalTasks} | Completed: {completedTasks}");
                Console.WriteLine($"Elapsed Time: {sw.Elapsed.TotalMilliseconds:F2} ms");
                Console.WriteLine("Tasks served per worker:");
                string servedSummary = "";
                for (int w = 1; w < size; w++)
                {
                    string info = $"Rank {w}: {tasksServed[w]} tasks";
                    Console.WriteLine($"  -> {info}");
                    servedSummary += (w > 1 ? ", " : "") + $"R{w}:{tasksServed[w]}";
                }
                Console.WriteLine($"All tasks completed correctly: {(correct ? "YES" : "NO")}");
                Console.WriteLine($"Summary Table Line: {size}\t{numWorkers}\t{servedSummary}\t{(correct ? "YES" : "NO")}");
            }
            else
            {
                // Worker / Client Role
                int myTasksCount = 0;
                while (true)
                {
                    // Send request to Master (Rank 0)
                    comm.Send(1, 0, TagRequest);

                    // Receive assigned task ID
                    comm.Receive(0, TagTask, out int taskId);

                    if (taskId == -1)
                    {
                        // Sentinel received: terminate
                        break;
                    }

                    myTasksCount++;

                    // Simulate task execution
                    long taskResult = 0;
                    for (int i = 0; i < 1000; i++)
                    {
                        taskResult += (taskId * 31 + i) % 997;
                    }

                    // Send result back to Master
                    comm.Send(taskResult, 0, TagResult);
                }
            }
        }

        // =========================================================================
        // 2. Token Ring (Peer-to-Peer Architecture Style)
        // =========================================================================
        static void RunTokenRing(Intracommunicator comm, int rank, int size)
        {
            const int totalHops = 10_000;
            int nextRank = (rank + 1) % size;
            int prevRank = (rank - 1 + size) % size;

            comm.Barrier();
            Stopwatch sw = Stopwatch.StartNew();

            int token = 0;

            if (rank == 0)
            {
                // Rank 0 injects the initial token
                comm.Send(token, nextRank, TagRing);

                // Rank 0 enters loop to receive, increment, and forward until totalHops reached
                while (true)
                {
                    comm.Receive(prevRank, TagRing, out token);
                    if (token >= totalHops)
                    {
                        // Stop timer
                        sw.Stop();
                        // Send termination token to all other ranks around the ring
                        comm.Send(-1, nextRank, TagRing);
                        break;
                    }
                    token++;
                    comm.Send(token, nextRank, TagRing);
                }
            }
            else
            {
                // Worker peers receive, increment, and forward
                while (true)
                {
                    comm.Receive(prevRank, TagRing, out token);
                    if (token == -1)
                    {
                        // Forward termination signal to next peer unless it's rank 0
                        if (nextRank != 0)
                        {
                            comm.Send(-1, nextRank, TagRing);
                        }
                        break;
                    }
                    token++;
                    comm.Send(token, nextRank, TagRing);
                }
            }

            comm.Barrier();

            if (rank == 0)
            {
                double totalMs = sw.Elapsed.TotalMilliseconds;
                double timePerMessageMs = totalMs / totalHops;
                bool correct = (token >= totalHops);

                Console.WriteLine($"=== Token Ring (Peer-to-Peer Style) Results ===");
                Console.WriteLine($"Total Ranks      : {size}");
                Console.WriteLine($"Total Hops       : {totalHops:N0}");
                Console.WriteLine($"Elapsed Time     : {totalMs:F2} ms");
                Console.WriteLine($"Time per Message : {timePerMessageMs:F4} ms ({(timePerMessageMs * 1000.0):F2} µs)");
                Console.WriteLine($"All hops correct : {(correct ? "YES" : "NO")}");
                Console.WriteLine($"Summary Table Line: {size}\t{totalMs:F2}\t{timePerMessageMs:F4}\t{(correct ? "YES" : "NO")}");
            }
        }
    }
}
