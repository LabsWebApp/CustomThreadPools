﻿namespace Pool;

public class InstanceThreadPool
{
    private readonly string? _name;
    private readonly Thread[] _threads;
    private readonly ThreadPriority _priority;

    private readonly Queue<(Action<object?> work, object? parameter)> _works = new();
    private readonly AutoResetEvent _workingEvent = new(false);
    private readonly AutoResetEvent _executeEvent = new(true);

    public string Name => _name ?? GetHashCode().ToString("x");

    public InstanceThreadPool(int maxThreadsCount, ThreadPriority priority = ThreadPriority.Normal, string? name = null)
    {
        if (maxThreadsCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxThreadsCount), maxThreadsCount, "Число потоков в пуле должно быть больше, либо равно 1");

        _priority = priority;
        _name = Name;
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

    public void Execute(Action work) => Execute(_ => work(), null);

    public void Execute(Action<object?> work, object? parameter)
    {
        // запрашиваем доступ к очереди
        _executeEvent.WaitOne();

        // добавляем в очередь действие 
        _works.Enqueue((work, parameter));
        // разрешаем доступ к очереди
        _executeEvent.Set();
        
        _workingEvent.Set();
    }

    private void WorkingThread()
    {
        throw new NotImplementedException();
    }
}