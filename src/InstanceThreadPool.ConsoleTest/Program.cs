using System.Diagnostics;
using System.Numerics;
using BenchmarkDotNet.Running;
using InstanceThreadPool.ConsoleTest;
using Pool;
using static System.Console;



BenchmarkRunner.Run<PoolBenchmark>();
ReadKey();
System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
SimpleThreadPool p = new ();

var cos = p.AddWorkEx(
    Math.Cos, 
    (x, y) => x + y, 0d, 2 * Math.PI, 4);
//WriteLine($"{sin:0.##############################}");
//ReadKey();
WriteLine($"{cos:0.##############################}");
ReadKey();
WriteLine(p.MaxThreadsCount);


BenchmarkRunner.Run<PoolBenchmark>();
ReadKey();
const int test = 10;
var pool = new SimpleThreadPool();

Stopwatch sw = Stopwatch.StartNew();
BigInteger r1 = pool.QuickFactorial(PoolBenchmark.Test);
BigInteger r2 = pool.AddWork(
    x => x,
    (b1, b2) => b1 * b2,
    BigInteger.One,
    PoolBenchmark.Test);
sw.Stop();


WriteLine(sw.ElapsedMilliseconds);
sw.Restart();
for (int i = 0; i < test; i++)
{
    r2 = pool.AddWork(
        x => x,
        (b1, b2) => b1 * b2,
        BigInteger.One,
        PoolBenchmark.Test);
}
sw.Stop();

WriteLine("New: " + sw.ElapsedMilliseconds / test);
sw.Restart();
for (int i = 0; i < test; i++)
{
    r1 = pool.QuickFactorial(PoolBenchmark.Test);
}
sw.Stop();
pool.Dispose();
WriteLine("Old: " + sw.ElapsedMilliseconds / test);
WriteLine(r1==r2);
//WriteLine(r2);

//const int total = 2;
//Func<int, BigInteger> func = x => x;
//var res = func.Aggregate(
//    BigInteger.One, (x, y) => x * y, total);

//WriteLine(res);
//WriteLine(PoolBenchmark.SimpleFactorial(total));


ReadKey();
var messages = Enumerable.Range(1, 3).Select(i => $"Message-{i}");

var threadPool = new Pool.SimpleThreadPool(8);
//threadPool.DisposeThreadJoinTimeout = 10;
foreach (var message in messages)
    threadPool.Execute(() =>
    {
        //var msg = (string)obj!;
        WriteLine($">> Обработка сообщения: начата... ({message})");
        //Thread.Sleep(100);
        WriteLine($">> Обработка сообщения:  выполнена! ({message})");
    });
threadPool.Execute(() =>
{
    //var msg = (string)obj!;
    WriteLine($">> Обработка сообщения: начата...");
    //Thread.Sleep(100);
    WriteLine($">> Обработка сообщения:  выполнена!");
});
threadPool.Execute(() =>
{
    //var msg = (string)obj!;
    WriteLine($">> Обработка сообщения: начата...");
    //Thread.Sleep(100);
    WriteLine($">> Обработка сообщения:  выполнена!");
});
//Thread.Sleep(10);
//threadPool.Dispose();

ReadKey();