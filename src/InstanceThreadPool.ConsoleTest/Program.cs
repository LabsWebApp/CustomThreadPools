using Pool;
using static System.Console;

//System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
var messages = Enumerable.Range(1, 10000).Select(i => $"Message-{i}");

var threadPool = new InstanceThreadPool(8, name: "Обработчик сообщений");
threadPool.DisposeThreadJoinTimeout = 10;
foreach (var message in messages)
    threadPool.Execute(message, obj =>
    {
        var msg = (string)obj!;
        WriteLine($">> Обработка сообщения: {msg} начата...");
        Thread.Sleep(100);
        WriteLine($">> Обработка сообщения: {msg} выполнена!");
    });
//Thread.Sleep(10);
//threadPool.Dispose();

ReadKey();