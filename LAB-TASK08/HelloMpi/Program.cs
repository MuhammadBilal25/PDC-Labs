using System;
using MPI;

namespace HelloMpi
{
    class Program
    {
        static void Main(string[] args)
        {
            // 1. Initialize the MPI Environment
            using (new MPI.Environment(ref args))
            {
                // 2. Query communicator, rank, size, and processor name
                Intracommunicator comm = Communicator.world;
                int rank = comm.Rank;
                int size = comm.Size;
                string processorName = MPI.Environment.ProcessorName;

                // 3. Unordered direct output: Every process prints directly to console
                Console.WriteLine($"[Unordered] Hello from Rank {rank} of {size} on {processorName}");

                // Synchronize all processes before printing the ordered block
                comm.Barrier();

                if (rank == 0)
                {
                    Console.WriteLine("\n--- Ordered Report via Explicit Point-to-Point Messaging ---");
                }
                comm.Barrier();

                // 4. Ordered report: Each rank sends its message to Rank 0, which prints sequentially
                if (rank == 0)
                {
                    // Rank 0 reports for itself
                    Console.WriteLine($"[Ordered]   Rank 0 of {size} on {processorName}");

                    // Rank 0 receives report lines in strict rank order (1 to size - 1)
                    for (int src = 1; src < size; src++)
                    {
                        comm.Receive(src, 0, out string message);
                        Console.WriteLine(message);
                    }
                }
                else
                {
                    // Worker ranks construct their report line and send it to Rank 0
                    string reportLine = $"[Ordered]   Rank {rank} of {size} on {processorName}";
                    comm.Send(reportLine, 0, 0);
                }
            }
        }
    }
}
