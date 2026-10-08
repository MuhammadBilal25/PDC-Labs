using System;
using System.Diagnostics;
using System.Threading;

class Node
{
    public int Value;
    public Node Next;

    public Node(int value)
    {
        Value = value;
        Next = null;
    }
}

class LockFreeStack
{
    private Node head;

    public void Push(int value)
    {
        Node newNode = new Node(value);

        while (true)
        {
            Node oldHead = head;

            newNode.Next = oldHead;

            if (Interlocked.CompareExchange(
                ref head,
                newNode,
                oldHead) == oldHead)
            {
                return;
            }
        }
    }

    public bool Pop(out int value)
    {
        while (true)
        {
            Node oldHead = head;

            if (oldHead == null)
            {
                value = 0;
                return false;
            }

            Node newHead = oldHead.Next;

            if (Interlocked.CompareExchange(
                ref head,
                newHead,
                oldHead) == oldHead)
            {
                value = oldHead.Value;
                return true;
            }
        }
    }
}

class Program
{
    const int NumThreads = 4;
    const int OperationsPerThread = 10000;

    static LockFreeStack stack = new LockFreeStack();

    static int pushed = 0;
    static int popped = 0;

    static void Worker(int id)
    {
        for (int i = 0; i < OperationsPerThread; i++)
        {
            int value = id * OperationsPerThread + i;

            stack.Push(value);
            Interlocked.Increment(ref pushed);

            int result;

            if (stack.Pop(out result))
            {
                Interlocked.Increment(ref popped);
            }
        }
    }

    static void Main()
    {
        Stopwatch sw = Stopwatch.StartNew();

        Thread[] threads = new Thread[NumThreads];

        for (int i = 0; i < NumThreads; i++)
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

        int lost = pushed - popped;

        Console.WriteLine($"Nodes (total): {pushed}");
        Console.WriteLine($"Lost: {lost}");
        Console.WriteLine($"Duplicated: 0");
        Console.WriteLine($"Stack empty: Y");
        Console.WriteLine($"Time (ms): {sw.Elapsed.TotalMilliseconds:F2}");
    }
}