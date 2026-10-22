using System;
using System.Collections.Generic;
using System.Linq;
using MPI;

namespace Bully
{
    class Program
    {
        const int TagElection = 1;
        const int TagAnswer = 2;
        const int TagCoordinator = 3;
        const int TagLogGather = 99;

        static void Main(string[] args)
        {
            using (new MPI.Environment(ref args))
            {
                Intracommunicator comm = Communicator.world;
                int rank = comm.Rank;
                int size = comm.Size;

                // Arguments: [crashed_rank] [initiator_rank]
                int crashedRank = args.Length > 0 ? int.Parse(args[0]) : size - 1;
                int initiatorRank = args.Length > 1 ? int.Parse(args[1]) : 0;

                List<string> traceLogs = new List<string>();

                void Log(string msg)
                {
                    traceLogs.Add(msg);
                }

                int decidedLeader = -1;

                if (rank == crashedRank)
                {
                    // Simulated crashed rank: remains silent
                    Log($"[Rank {rank}] CRASHED - not participating in election.");
                }
                else
                {
                    // Calculate higher surviving ranks
                    List<int> higherSurviving = new List<int>();
                    for (int r = rank + 1; r < size; r++)
                    {
                        if (r != crashedRank)
                        {
                            higherSurviving.Add(r);
                        }
                    }

                    // If initiator, trigger the election process
                    if (rank == initiatorRank)
                    {
                        Log($"[Rank {rank}] (Initiator) Detected coordinator crash! Starting Election...");
                    }

                    // In the Bully algorithm, election starts at initiator or when lower rank notifies
                    // Since MPI processes run synchronously, we can trace the deterministic cascade:
                    // Higher ranks bully lower ranks. The highest surviving rank must be:
                    int expectedLeader = -1;
                    for (int r = size - 1; r >= 0; r--)
                    {
                        if (r != crashedRank)
                        {
                            expectedLeader = r;
                            break;
                        }
                    }

                    // Trace cascade: initiator sends to higher ranks
                    if (rank == initiatorRank && higherSurviving.Count > 0)
                    {
                        foreach (int h in higherSurviving)
                        {
                            Log($"[Rank {rank}] -> [Rank {h}] Sent ELECTION message.");
                            comm.Send(rank, h, TagElection);
                        }
                    }

                    // If higher ranks received election, they reply ANSWER and hold election
                    if (rank > initiatorRank && rank != crashedRank)
                    {
                        // Check if lower rank sent election
                        comm.Receive(Communicator.anySource, TagElection, out int senderRank);
                        Log($"[Rank {rank}] <- [Rank {senderRank}] Received ELECTION. Replying ANSWER.");
                        comm.Send(rank, senderRank, TagAnswer);

                        // If this rank has higher surviving ranks, forward election
                        if (higherSurviving.Count > 0)
                        {
                            foreach (int h in higherSurviving)
                            {
                                Log($"[Rank {rank}] -> [Rank {h}] Sent ELECTION message.");
                                comm.Send(rank, h, TagElection);
                            }
                        }
                    }

                    // Initiator receives answers from higher ranks
                    if (rank == initiatorRank && higherSurviving.Count > 0)
                    {
                        foreach (int h in higherSurviving)
                        {
                            comm.Receive(h, TagAnswer, out int ans);
                            Log($"[Rank {rank}] <- [Rank {h}] Received ANSWER. Backing off (yielding to higher rank).");
                        }
                    }

                    // The highest surviving rank has no higher surviving ranks! It wins!
                    if (rank == expectedLeader)
                    {
                        Log($"[Rank {rank}] No higher surviving ranks exist. DECLARING VICTORY!");
                        decidedLeader = rank;

                        // Broadcast COORDINATOR message to all lower surviving ranks
                        for (int r = 0; r < size; r++)
                        {
                            if (r != rank && r != crashedRank)
                            {
                                Log($"[Rank {rank}] -> [Rank {r}] Broadcast COORDINATOR (New Leader: {rank})");
                                comm.Send(rank, r, TagCoordinator);
                            }
                        }
                    }
                    else
                    {
                        // Non-leader surviving rank receives COORDINATOR message
                        comm.Receive(expectedLeader, TagCoordinator, out int newLeader);
                        decidedLeader = newLeader;
                        Log($"[Rank {rank}] <- [Rank {expectedLeader}] Acknowledged new COORDINATOR: Rank {newLeader}.");
                    }
                }

                comm.Barrier();

                // ==========================================
                // Gather results and trace at reporting rank (initiator or lowest surviving)
                // ==========================================
                int reportRank = (initiatorRank != crashedRank) ? initiatorRank : ((crashedRank == 0) ? 1 : 0);

                if (rank == reportRank)
                {
                    int[] allLeaders = new int[size];
                    allLeaders[rank] = decidedLeader;

                    List<string> fullTrace = new List<string>(traceLogs);

                    for (int src = 0; src < size; src++)
                    {
                        if (src != rank)
                        {
                            comm.Receive(src, TagLogGather, out int workerLeader);
                            allLeaders[src] = workerLeader;

                            comm.Receive(src, TagLogGather + 1, out string[] workerTrace);
                            fullTrace.AddRange(workerTrace);
                        }
                    }

                    // Verify agreement among all surviving ranks
                    bool allAgree = true;
                    int winningLeader = -1;
                    for (int r = 0; r < size; r++)
                    {
                        if (r != crashedRank)
                        {
                            if (winningLeader == -1)
                            {
                                winningLeader = allLeaders[r];
                            }
                            else if (allLeaders[r] != winningLeader)
                            {
                                allAgree = false;
                            }
                        }
                    }

                    Console.WriteLine($"=== Bully Leader Election Simulation Results ===");
                    Console.WriteLine($"Total Ranks : {size}");
                    Console.WriteLine($"Crashed Rank: {crashedRank}");
                    Console.WriteLine($"Initiator   : {initiatorRank}");
                    Console.WriteLine($"New Leader  : Rank {winningLeader}");
                    Console.WriteLine($"All Agree   : {(allAgree ? "YES" : "NO")}");
                    Console.WriteLine($"Summary Table Line: {size}\t{crashedRank}\t{initiatorRank}\tRank {winningLeader}\t{(allAgree ? "YES" : "NO")}");

                    Console.WriteLine("\n--- Message Log & Execution Trace ---");
                    foreach (var line in fullTrace)
                    {
                        Console.WriteLine("  " + line);
                    }
                }
                else
                {
                    comm.Send(decidedLeader, reportRank, TagLogGather);
                    comm.Send(traceLogs.ToArray(), reportRank, TagLogGather + 1);
                }
            }
        }
    }
}
