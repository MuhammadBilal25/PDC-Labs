using System;
using System.Collections.Generic;
using System.Linq;
using MPI;

namespace Lamport
{
    [Serializable]
    public struct ClockMessage
    {
        public int MsgId;
        public int Sender;
        public int Timestamp;
    }

    [Serializable]
    public struct EventRecord
    {
        public int Rank;
        public string Type; // "LOCAL", "SEND", "RECV"
        public int MsgId;
        public int Clock;
        public int OtherRank;
        public int BeforeClock;
    }

    class Program
    {
        static void Main(string[] args)
        {
            using (new MPI.Environment(ref args))
            {
                Intracommunicator comm = Communicator.world;
                int rank = comm.Rank;
                int size = comm.Size;

                int logicalClock = 0;
                List<EventRecord> localLogs = new List<EventRecord>();
                List<Request> pendingSends = new List<Request>();

                // Rule 1: Local event
                void LocalEvent()
                {
                    logicalClock++;
                    localLogs.Add(new EventRecord
                    {
                        Rank = rank,
                        Type = "LOCAL",
                        MsgId = 0,
                        Clock = logicalClock,
                        OtherRank = -1,
                        BeforeClock = logicalClock - 1
                    });
                }

                // Rule 2: Send event
                void SendEvent(int destRank, int msgId)
                {
                    logicalClock++;
                    localLogs.Add(new EventRecord
                    {
                        Rank = rank,
                        Type = "SEND",
                        MsgId = msgId,
                        Clock = logicalClock,
                        OtherRank = destRank,
                        BeforeClock = logicalClock - 1
                    });

                    ClockMessage msg = new ClockMessage
                    {
                        MsgId = msgId,
                        Sender = rank,
                        Timestamp = logicalClock
                    };

                    // Use non-blocking ImmediateSend to eliminate deadlocks in distributed rings
                    Request req = comm.ImmediateSend(msg, destRank, 10);
                    pendingSends.Add(req);
                }

                // Rule 3: Receive event (clock = max(localClock, msgClock) + 1)
                void ReceiveEvent(int srcRank)
                {
                    comm.Receive(srcRank, 10, out ClockMessage msg);
                    int before = logicalClock;
                    logicalClock = Math.Max(logicalClock, msg.Timestamp) + 1;

                    localLogs.Add(new EventRecord
                    {
                        Rank = rank,
                        Type = "RECV",
                        MsgId = msg.MsgId,
                        Clock = logicalClock,
                        OtherRank = msg.Sender,
                        BeforeClock = before
                    });
                }

                // Sequence of distributed events across ranks
                // 1. Initial local processing
                LocalEvent();

                // 2. Ring messaging: each rank sends to (rank + 1) % size
                int nextRank = (rank + 1) % size;
                int prevRank = (rank - 1 + size) % size;
                int ringMsgId = rank * 100 + 1;

                SendEvent(nextRank, ringMsgId);
                LocalEvent();
                ReceiveEvent(prevRank);

                // 3. Skip messaging: each rank sends to (rank + 2) % size
                int skipTarget = (rank + 2) % size;
                int skipSource = (rank - 2 + size) % size;
                int skipMsgId = rank * 100 + 2;

                SendEvent(skipTarget, skipMsgId);
                LocalEvent();
                ReceiveEvent(skipSource);

                LocalEvent();

                // Wait for all non-blocking sends to complete
                foreach (var req in pendingSends)
                {
                    req.Wait();
                }

                comm.Barrier();

                // ==========================================
                // Merge Logs at Rank 0
                // ==========================================
                EventRecord[] localArray = localLogs.ToArray();

                if (rank == 0)
                {
                    List<EventRecord> allEvents = new List<EventRecord>(localArray);

                    for (int src = 1; src < size; src++)
                    {
                        comm.Receive(src, 99, out EventRecord[] workerLogs);
                        allEvents.AddRange(workerLogs);
                    }

                    // Sort events by (Clock, Rank) for Lamport Total Ordering
                    var sortedEvents = allEvents.OrderBy(e => e.Clock).ThenBy(e => e.Rank).ToList();

                    // Check Causal Consistency: for every message, send.Clock < recv.Clock
                    var sendEvents = allEvents.Where(e => e.Type == "SEND").ToDictionary(e => e.MsgId);
                    var recvEvents = allEvents.Where(e => e.Type == "RECV").ToDictionary(e => e.MsgId);

                    int totalMessages = sendEvents.Count;
                    int violations = 0;

                    foreach (var kvp in sendEvents)
                    {
                        int id = kvp.Key;
                        var sEvent = kvp.Value;
                        if (recvEvents.TryGetValue(id, out var rEvent))
                        {
                            if (sEvent.Clock >= rEvent.Clock)
                            {
                                violations++;
                            }
                        }
                    }

                    bool isConsistent = (violations == 0);

                    Console.WriteLine($"=== Lamport Logical Clock Consistency Results ===");
                    Console.WriteLine($"Ranks           : {size}");
                    Console.WriteLine($"Total Events    : {sortedEvents.Count}");
                    Console.WriteLine($"Total Messages  : {totalMessages}");
                    Console.WriteLine($"Causal Violation: {violations}");
                    Console.WriteLine($"Consistent      : {(isConsistent ? "YES" : "NO")}");
                    Console.WriteLine($"Summary Table Line: {size}\t{sortedEvents.Count}\t{totalMessages}\t{violations}\t{(isConsistent ? "YES" : "NO")}");

                    // Pick first message to trace
                    if (totalMessages > 0)
                    {
                        var firstSend = sendEvents.Values.OrderBy(e => e.MsgId).First();
                        var firstRecv = recvEvents[firstSend.MsgId];
                        Console.WriteLine("\n--- Followed Message Trace ---");
                        Console.WriteLine($"Message ID          : {firstSend.MsgId}");
                        Console.WriteLine($"Sender Rank         : {firstSend.Rank}");
                        Console.WriteLine($"Send Clock          : {firstSend.Clock}");
                        Console.WriteLine($"Receiver Rank       : {firstRecv.Rank}");
                        Console.WriteLine($"Clock Before Receive: {firstRecv.BeforeClock}");
                        Console.WriteLine($"Receive Clock       : {firstRecv.Clock}");
                        Console.WriteLine($"Rule Applied        : clock = max(beforeClock, sendClock) + 1 => max({firstRecv.BeforeClock}, {firstSend.Clock}) + 1 = {firstRecv.Clock}");
                        Console.WriteLine($"Trace Table Line: {firstSend.Rank}\t{firstSend.Clock}\t{firstRecv.Rank}\t{firstRecv.BeforeClock}\t{firstRecv.Clock}\tclock=max({firstRecv.BeforeClock},{firstSend.Clock})+1={firstRecv.Clock}");
                    }
                }
                else
                {
                    comm.Send(localArray, 0, 99);
                }
            }
        }
    }
}
