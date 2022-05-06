using System.Diagnostics;

namespace DebugPool;

public class InstanceThreadPool : IDisposable
{
    private readonly string? _name;
    private readonly Thread[] _threads;
    private readonly ThreadPriority _priority;

    private readonly Queue<(Action<object?> work, object? parameter)> _works = new();
    private readonly AutoResetEvent _workingEvent = new(false);
    private readonly AutoResetEvent _queueEvent = new(true);
    
    private volatile bool _canWork = true;
    private int _disposeThreadJoinTimeout = 1;
    private bool _disposedValue;

    public int DisposeThreadJoinTimeout
    {
        get => _disposeThreadJoinTimeout;
        set
        {
            if (value < 0) 
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, 
                    "Таймаут не может быть меньше 0.");
            _disposeThreadJoinTimeout = value;
        }
    } 

    public string Name => _name ?? GetHashCode().ToString("x");

    public InstanceThreadPool(
        int maxThreadsCount = 0, 
        ThreadPriority priority = ThreadPriority.Normal,
        string? name = null)
    {
        if (maxThreadsCount <= 0) maxThreadsCount = Environment.ProcessorCount;

        _priority = priority;
        _name = name;
        _threads = new Thread[maxThreadsCount];

        Initialize();
    }

    private void Initialize()
    {
        for (var i = 0; i < _threads.Length; i++)
        {
            var name = $"{nameof(InstanceThreadPool)}[{Name}]-Thread[{i}]";
            var thread = new Thread(WorkingThread)
            {
                Name = name,
                IsBackground = true,
                Priority = _priority
            };
            _threads[i] = thread;
            thread.Start();
        }
    }

    public void Execute(Action work) => Execute(null, _ => work());

    public void Execute(object? parameter, Action<object?> work)
    {
        if (!_canWork) throw new InvalidOperationException(
            "Попытка передать задание уничтоженному пулу потоков");

        // запрашиваем доступ к очереди
        _queueEvent.WaitOne();

        if (!_canWork) throw new InvalidOperationException(
            "Попытка передать задание уничтоженному пулу потоков");

        // добавляем в очередь действие 
        _works.Enqueue((work, parameter));
        
        // разрешаем доступ к очереди
        _queueEvent.Set();
        
        _workingEvent.Set();
    }

    private void WorkingThread()
    {
        var threadName = Thread.CurrentThread.Name;
        Trace.TraceInformation($"Поток {threadName} запущен с ID: {Environment.CurrentManagedThreadId}");

        try
        {
            while (_canWork)
            {
                // дожидаемся разрешения на выполнение
                _workingEvent.WaitOne();
                if (!_canWork) break;

                // запрашиваем доступ к очереди
                _queueEvent.WaitOne();

                // до тех пор пока в очереди нет заданий
                while (_works.Count == 0)
                {
                    // освобождаем очередь
                    _queueEvent.Set();

                    // дожидаемся разрешения на выполнение
                    _workingEvent.WaitOne();
                    if (!_canWork) break;

                    // запрашиваем доступ к очереди вновь
                    _queueEvent.WaitOne();
                }

                var (work, parameter) = _works.Dequeue();

                // если после изъятия из очереди задания там осталось ещё что-то
                if (_works.Count > 0)
                    //  то запускаем ещё один поток на выполнение
                    _workingEvent.Set();

                // разрешаем доступ к очереди
                _queueEvent.Set();

                Trace.TraceInformation($"Поток {threadName}[id:{Environment.CurrentManagedThreadId}] выполняет задание");
                try
                {
                    var timer = Stopwatch.StartNew();
                    work(parameter);
                    timer.Stop();

                    Trace.TraceInformation(
                        $"Поток {threadName}[id:{Environment.CurrentManagedThreadId}] выполнил задание за {timer.ElapsedMilliseconds}мс");
                }
                catch (Exception e)
                {
                    if (e is ThreadInterruptedException) throw;
                    Trace.TraceError($"Ошибка выполнения задания в потоке {threadName}:{e}");
                }
            }
        }
        catch (ThreadInterruptedException)
        {
            Trace.TraceWarning(
                $"Поток {threadName} был принудительно прерван при завершении работы пула \"{Name}\"");
        }
        finally
        {
            Trace.TraceInformation($"Поток {threadName} завершил свою работу");
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
            Trace.TraceInformation($"Пул потоков \"{Name}\" уничтожен");
        }
        _disposedValue = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}