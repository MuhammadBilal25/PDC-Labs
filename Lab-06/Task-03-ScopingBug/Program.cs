using System;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static void Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "local";
        int degree = args.Length > 1 && int.TryParse(args[1], out int parsed)
            ? parsed : Environment.ProcessorCount;

        const int N = 100_000;
        int[] input = new int[N];
        int[] output = new int[N];

        for (int i = 0; i < N; i++)
            input[i] = i;

        if (mode == "bug")
        {
            int tmp = 0; // Shared variable: this is intentionally incorrect.
            Parallel.For(0, N, new ParallelOptions { MaxDegreeOfParallelism = degree }, i =>
            {
                tmp = input[i] * 3;
                Thread.SpinWait(20);
                output[i] = tmp + 7;
            });
        }
        else if (mode == "threadlocal")
        {
            Parallel.For(0, N,
                new ParallelOptions { MaxDegreeOfParallelism = degree },
                () => 0,
                (i, state, tmp) =>
                {
                    tmp = input[i] * 3;
                    Thread.SpinWait(20);
                    output[i] = tmp + 7;
                    return tmp;
                },
                tmp => { });
        }
        else if (mode == "local")
        {
            Parallel.For(0, N, new ParallelOptions { MaxDegreeOfParallelism = degree }, i =>
            {
                int tmp = input[i] * 3; // Private to this iteration.
                Thread.SpinWait(20);
                output[i] = tmp + 7;
            });
        }
        else
        {
            Console.WriteLine("Usage: dotnet run -- bug|threadlocal|local [degree]");
            return;
        }

        int wrong = 0;
        for (int i = 0; i < N; i++)
        {
            if (output[i] != input[i] * 3 + 7)
                wrong++;
        }

        Console.WriteLine($"Mode = {mode}, degree = {degree}");
        Console.WriteLine($"Wrong elements = {wrong}");
        Console.WriteLine($"Environment.ProcessorCount = {Environment.ProcessorCount}");

        // Static-lambda experiment: temporarily change the bug lambda to:
        // Parallel.For(0, N, static i => { tmp = input[i] * 3; });
        // It fails to compile because a static lambda cannot capture local variables.
    }
}
