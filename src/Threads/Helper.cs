using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;

namespace Pool;

public static partial class Helper
{
    public static T AddWork<T>(
        this SimpleThreadPool pool,
        Func<int, T> func,
        Func<T, T, T> accumulate,
        T seed,
        int total)
    {
        if (func == null) throw new ArgumentNullException(nameof(func));
        if (accumulate == null) throw new ArgumentNullException(nameof(accumulate));

        switch (total)
        {
            case < 1:
                throw new ArgumentOutOfRangeException(nameof(total), total,
                    "Общее количество вычислений не может быть меньше одного.");
            case 1:
                return accumulate(seed, func(1));
        }
        var processes = Math.Min(total, pool.MaxThreadsCount);

        Stack<T> result = new();

        var stackEvent = new AutoResetEvent(true);
        var accEvent = new AutoResetEvent(false);
        var finish = 1;

        void VoidAccumulation(int start)
        {
            var res = seed;
            for (var i = start; i <= total; i += processes)
                res = accumulate(res, func(i));

            stackEvent!.WaitOne();
            if (result.Count > 0)
            {
                var pop = result.Pop();
                stackEvent.Set();

                InnerLoop:
                res = accumulate(res, pop);
                Interlocked.Increment(ref finish);
                stackEvent.WaitOne();
                if (result.Count > 0)
                {
                    pop = result.Pop();
                    stackEvent.Set();
                    goto InnerLoop;
                }
                result.Push(res);
                if (finish == processes) accEvent!.Set();
                else stackEvent.Set();
            }
            else
            {
                result.Push(res);
                stackEvent.Set();
            }
        }

        void StartNew(int i) => pool.Execute(() => VoidAccumulation(i));
        var i = 1;
        Loop:
        StartNew(i);
        if (++i <= processes) goto Loop;
        
        accEvent.WaitOne();
        accEvent.Dispose();
        stackEvent.Dispose();
        return result.Pop();
    }

    public static T AddWork<T>(
       this SimpleThreadPool pool,
       Func<double, T> func,
       Func<T, T, T> accumulate,
       T seed,
       double range,
       int total)
    {
        if (func == null) throw new ArgumentNullException(nameof(func));
        if (accumulate == null) throw new ArgumentNullException(nameof(accumulate));
        if (range < 1)
            throw new ArgumentOutOfRangeException(nameof(range), range,
                "Диапазон для вычислений должен быть строго больше ноля.");

        switch (total)
        {
            case < 0:
                throw new ArgumentOutOfRangeException(nameof(total), total,
                    "Шагов для вычислений должно быть строго больше ноля.");
            case 1:
                return accumulate(seed, func(0));
            case 2:
                return accumulate(accumulate(seed, func(0)), func(range));
        }

        var delta = range / (total - 1);
        var processes = Math.Min(total, pool.MaxThreadsCount);
        var step = delta * processes;

        Stack<T> result = new();

        var stackEvent = new AutoResetEvent(true);
        var accEvent = new AutoResetEvent(false);
        var finish = 1;

        void VoidAccumulation(int start)
        {
            var res = seed;
            for (var d = start * delta; d <= range; d += step) 
            {
                pool.Execute(d, dd => Trace.TraceInformation(
                    new Tuple<double, T>((double)dd!,
                        func((double)dd)).ToString()));

                res = accumulate(res, func(d));
            }

            stackEvent!.WaitOne();
            if (result.Count > 0)
            {
                var pop = result.Pop();
                stackEvent.Set();

                InnerLoop:
                res = accumulate(res, pop);
                Interlocked.Increment(ref finish);
                Trace.TraceInformation(new Tuple<int, T, T>(finish, res, pop) + "   !!!!!");

                stackEvent.WaitOne();
                if (result.Count > 0)
                {
                    pop = result.Pop();
                    stackEvent.Set();
                    goto InnerLoop;
                }
                result.Push(res);
                if (finish == processes) accEvent!.Set();
                else stackEvent.Set();
            }
            else
            {
                result.Push(res);
                stackEvent.Set();
            }
        }

        void StartNew(int i) => pool.Execute(() => VoidAccumulation(i));
        var i = 0;
    Loop:
        StartNew(i);
        if (i++ < processes) goto Loop;
        
        accEvent.WaitOne();
        accEvent.Dispose();
        accEvent.Dispose();

        return result.Pop();
    }

    public static T AddWorkEx<T>(
       this SimpleThreadPool pool,
       Func<double, T> func,
       Func<T, T, T> accumulate,
       T seed,
       double range,
       int total)
    {
        if (func == null) throw new ArgumentNullException(nameof(func));
        if (accumulate == null) throw new ArgumentNullException(nameof(accumulate));
        if (range < 1)
            throw new ArgumentOutOfRangeException(nameof(range), range,
                "Диапазон для вычислений должен быть строго больше ноля.");

        switch (total)
        {
            case < 0:
                throw new ArgumentOutOfRangeException(nameof(total), total,
                    "Шагов для вычислений должно быть строго больше ноля.");
            case 1:
                return accumulate(seed, func(0));
            case 2:
                return accumulate(accumulate(seed, func(0)), func(range));
        }

        var delta = range / (total - 1);
        var processes = Math.Min(total, pool.MaxThreadsCount);
        var step = delta * processes;

        ConcurrentBag<T?> result = new();

        var accEvent = new AutoResetEvent(false);
        var addsEvent = new AutoResetEvent(false);
        var finish = 1;
        var adds = 0;

        void AddAccumulation(T res, bool noAdd = true)
        {
            void AddsIncrement()
            {
                Interlocked.Increment(ref adds);
                if (adds == processes) addsEvent!.Set();
            }

            if (result.TryTake(out var take))
            {
                do
                {
                    res = accumulate(res, take!);
                    Interlocked.Increment(ref finish);
                    if (noAdd)
                    {

                    }
                    //Trace.TraceInformation(new Tuple<int, T, T?>(finish, res, take) + "   !!!!!");
                } while (result.TryTake(out take));

                result.Add(res);
                AddsIncrement();
                if (finish == processes) accEvent!.Set();
            }
            else
            {
                result.Add(res);
                AddsIncrement();
                //Trace.TraceInformation(res + "   !!!!!");
            }
        }

        void Accumulation(int start)
        {
            var res = seed;
            for (var d = start * delta; d <= range; d += step)
                res = accumulate(res, func(d));
            AddAccumulation(res);
        }

        void StartNew(int i) => pool.Execute(() => Accumulation(i));
        for (var i = 0; i < processes; i++)
        {
            if (i == processes - 1) Thread.Sleep(1);
            StartNew(i);
        }

        accEvent.WaitOne();
        pool.Execute(accEvent.Dispose); 

        if (result.TryTake(out var endResult)) return endResult!;
        throw new NotImplementedException();
    }

    public static BigInteger QuickFactorial(this SimpleThreadPool pool, int total)
    {
        switch (total)
        {
            case < 1:
                throw new ArgumentOutOfRangeException(nameof(total), total,
                    "Общее количество вычислений не может быть меньше одного.");
            case 1:
                return BigInteger.One;
        }

        var processes = Math.Min(total, pool.MaxThreadsCount);

        Stack<BigInteger> result = new();

        using var stackEvent = new AutoResetEvent(true);
        using var accEvent = new AutoResetEvent(false);
        var finish = 1;

        void VoidMultiplication(int start)
        {
            var res = BigInteger.One;
            for (var i = start; i <= total; i += processes)
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
                if (finish == processes) accEvent!.Set();
                else stackEvent.Set();
            }
            else
            {
                result.Push(res);
                stackEvent.Set();
            }
        }

        void StartNew(int i) => pool.Execute(() => VoidMultiplication(i));
        var i = 1;
        Loop:
        StartNew(i);
        if (++i <= processes) goto Loop;
        accEvent.WaitOne();

        return result.Pop();
    }
}