#if DEBUG
using System.Diagnostics;
#endif

namespace Pool;

public class SimpleThreadPool : IDisposable
{
    public readonly int MaxThreadsCount;

    private readonly Thread[] _threads;
    private readonly ThreadPriority _priority;

    private readonly Queue<(Action<object?> work, object? parameter)> _works = new();
    private readonly AutoResetEvent _workingEvent = new(false);
    private readonly AutoResetEvent _queueEvent = new(true);

    private volatile bool _canWork = true;
    private int _disposeThreadJoinTimeout = 100;
    private bool _disposedValue;

    public int DisposeThreadJoinTimeout
    {
        get => _disposeThreadJoinTimeout;
        set => _disposeThreadJoinTimeout = _canWork switch
        {
            true => value < 1 ? 100 : value,
            _ => throw new InvalidOperationException(
                $"Попытка изменить поле \"{nameof(DisposeThreadJoinTimeout)}\" уничтоженному пулу потоков")
        };
    }

    public SimpleThreadPool(
        int maxThreadsCount = 0,
        ThreadPriority priority = ThreadPriority.Normal)
    {
        MaxThreadsCount = maxThreadsCount <= 0 ? Environment.ProcessorCount : maxThreadsCount;

        _priority = priority;
        _threads = new Thread[MaxThreadsCount];

        Initialize();
    }

    public void Execute(Action work) => Execute(null, _ => work());

    public void Execute(object? parameter, Action<object?> work)
    {
        if (!_canWork) throw new InvalidOperationException(
            "Попытка передать задание уничтоженному пулу потоков");

        _queueEvent.WaitOne();

        if (!_canWork) throw new InvalidOperationException(
            "Попытка передать задание уничтоженному пулу потоков");

        _works.Enqueue((work, parameter));
        _queueEvent.Set();
        _workingEvent.Set();
    }

    private void Initialize()
    {
        for (var i = 0; i < _threads.Length; i++)
        {
            var thread = new Thread(WorkingThread)
            {
                IsBackground = true,
                Priority = _priority
            };
            _threads[i] = thread;
            thread.Start();
        }
    }

    private void WorkingThread()
    {
        try
        {
            while (_canWork)
            {
                _workingEvent.WaitOne();
                if (!_canWork) break;

                _queueEvent.WaitOne();

                while (_works.Count == 0)
                {
                    if (!_canWork) break;
                    _queueEvent.Set();

                    _workingEvent.WaitOne();
                    if (!_canWork) break;

                    _queueEvent.WaitOne();
                }

                var (work, parameter) = _works.Dequeue();

                if (_works.Count > 0) _workingEvent.Set();

                _queueEvent.Set();
                work(parameter);
            }
        }
        catch (ThreadInterruptedException)
        {
#if DEBUG
            Trace.TraceWarning(
                "Поток был принудительно прерван при завершении работы пула.");
#endif
        }
        catch (Exception e)
        {
            if (_canWork)
            {
#if DEBUG
            Trace.TraceError(
                $"Возникла ошибка при выполнении одного из заданий: \"{e.Message}\".");
#endif
                throw;
                //WorkingThread();
            }
        }
        finally
        {
            _workingEvent.Set();
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;

        if (disposing)
        {
            _canWork = false;

            _workingEvent.Set();
            foreach (var thread in _threads)
                if (!thread.Join(DisposeThreadJoinTimeout))
                    thread.Interrupt();

            foreach (var thread in _threads)
                thread.Join();

            _queueEvent.Dispose();
            _workingEvent.Dispose();
        }
        _disposedValue = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}