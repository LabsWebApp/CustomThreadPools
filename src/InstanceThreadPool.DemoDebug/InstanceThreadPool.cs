using System.Diagnostics;

namespace Pool;

public class InstanceThreadPool
{
    private readonly string? _name;
    private readonly Thread[] _threads;
    private readonly ThreadPriority _priority;

    private readonly Queue<(Action<object?> work, object? parameter)> _works = new();
    private readonly AutoResetEvent _workingEvent = new(false);
    private readonly AutoResetEvent _queueEvent = new(true);

    public string Name => _name ?? GetHashCode().ToString("x");

    public InstanceThreadPool(int maxThreadsCount, ThreadPriority priority = ThreadPriority.Normal, string? name = null)
    {
        if (maxThreadsCount <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maxThreadsCount), 
                maxThreadsCount, 
                "Число потоков в пуле должно быть больше либо равно 1");

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
        // запрашиваем доступ к очереди
        _queueEvent.WaitOne(); 

        // добавляем в очередь действие 
        _works.Enqueue((work, parameter));
        
        // разрешаем доступ к очереди
        _queueEvent.Set();
        
        _workingEvent.Set();
    }

    private void WorkingThread()
    {
        var name = Thread.CurrentThread.Name;

        while (true)
        {
            // дожидаемся разрешения на выполнение
            _workingEvent.WaitOne();
             
            // запрашиваем доступ к очереди
            _queueEvent.WaitOne();

            // до тех пор пока в очереди нет заданий
            while (_works.Count == 0) 
            {
                // освобождаем очередь
                _queueEvent.Set();
                // дожидаемся разрешения на выполнение
                _workingEvent.WaitOne();
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

            Trace.TraceInformation($"Поток {name}[id:{Environment.CurrentManagedThreadId}] выполняет задание");
            try
            {
                work(parameter);
            }
            catch (Exception e)
            {
                Trace.TraceError($"Ошибка выполнения задания в потоке {name}:{e}");
            }
        }
    }
}