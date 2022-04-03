using Pool;
using static System.Console;

var messages = Enumerable.Range(1, 100).Select(i => $"Message-{i}");
WriteLine("init start");
var threadPool = new InstanceThreadPool(1024, name: "Обработчик сообщений");
WriteLine("init finish");
ReadKey();
foreach (var message in messages)
    threadPool.Execute(message, obj =>
    {
        var msg = (string)obj!;
        WriteLine($">> Обработка сообщения: {msg} начата...");
        Thread.Sleep(100);
        WriteLine($">> Обработка сообщения: {msg} выполнена!");
    });

Console.ReadKey();