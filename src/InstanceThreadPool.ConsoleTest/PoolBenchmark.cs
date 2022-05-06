using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Mathematics;
using Pool;

namespace InstanceThreadPool.ConsoleTest;

//[SimpleJob(RunStrategy.ColdStart, launchCount: 1, warmupCount: 5, targetCount: 5, id: "FastAndDirtyJob")]
[MemoryDiagnoser]
[RankColumn(NumeralSystem.Roman)]
public class PoolBenchmark : IDisposable
{
    public const int Test = 100000;

    private readonly SimpleThreadPool _pool = new();
    private readonly SimpleThreadPool _pool1024 = new(1024);

    //[Benchmark(Description = "SimpleFactorial")]
    public void SimpleFactorialTest()
    {
        var _ = SimpleFactorial(Test);
    }

    //[Benchmark(Description = "SimpleThreadPool1024")]
    public void SimpleThreadPool1024QuickFactorialTest()
    {
        var _ = _pool1024.AddWork(
            x => x,
            (b1, b2) => b1 * b2,
            BigInteger.One,
            PoolBenchmark.Test);
    }

    //[Benchmark(Description = "SimpleThreadPool")]
    public void SimpleThreadPoolQuickFactorialTest()
    {
        var _ = _pool.AddWork(
            x => x,
            (b1, b2) => b1 * b2,
            BigInteger.One,
            PoolBenchmark.Test);
    }

    //[Benchmark(Description = "Factorial")]
    public void QuickFactorialTest()
    {
        var _ = QuickFactorial(Test);
    }

    //[Benchmark(Description = "Factorial1024")]
    public void QuickFactorial1024Test()
    {
        var _ = QuickFactorial(Test, 1024);
    }
    //[Benchmark(Description = "SimpleThreadPoo")]
    //public void SimpleThreadPoolTest()
    //{
    //    //Func<int, BigInteger> func = x => x;
    //    //BigInteger Acc(BigInteger x, BigInteger y) => x * y;
    //    //int Adapter(int x) => x;

    //    //var res = func.Aggregate<int, BigInteger>(BigInteger.One, Acc, test, Adapter);
    //    //Result = res;
    //}

    public static BigInteger QuickFactorial(int x, int processes = 0)
    {
        processes = processes <= 0 ? Environment.ProcessorCount : processes;
        if (x < processes) return SimpleFactorial(x);

        Stack<BigInteger> result = new();

        using var stackEvent = new AutoResetEvent(true);
        using var multiplyEvent = new AutoResetEvent(false);
        var finish = 1;

        void VoidMultiplication(int start)
        {
            var res = BigInteger.One;
            for (var i = start; i <= x; i += processes)
                res *= i;

            stackEvent!.WaitOne();
            if (result.Count > 0)
            {
                var popRes = result.Pop();
                stackEvent!.Set();

                InnerLoop:
                res *= popRes;
                Interlocked.Increment(ref finish);
                stackEvent.WaitOne();
                if (result.Count > 0)
                {
                    popRes = result.Pop();
                    stackEvent.Set();
                    goto InnerLoop;
                }
                result.Push(res);
                if (finish == processes) multiplyEvent!.Set();
                else stackEvent.Set();
            }
            else
            {
                result.Push(res);
                stackEvent.Set();
            }
        }

        void StartNew(int i) => Task.Factory.StartNew(
            () => VoidMultiplication(i),
            TaskCreationOptions.LongRunning);

        var i = 1;
        Loop:
        StartNew(i);
        if (++i <= processes) goto Loop;

        multiplyEvent.WaitOne();
        stackEvent.Dispose();
        multiplyEvent.Dispose();
        return result.Pop();
    }

    public static BigInteger SimpleFactorial(int x)
    {
        var result = BigInteger.One;
        for (var i = 2; i <= x; i++) result *= i;
        return result;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);  
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pool.Dispose();
            _pool1024.Dispose();
        }
    }
}