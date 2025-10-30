using System.Diagnostics;

namespace SimpleBinarySerialization
{
    internal class Program
    {
        const int THREADS = 16;
        const int LOOPS_PER_THREAD = 12500000 / 2; // 8 x 1250w = 1亿
        static int checkOk = 0;

        static void Main(string[] args)
        {
            var sw = Stopwatch.StartNew();

            Task[] tasks = new Task[THREADS];
            for (int t = 0; t < THREADS; ++t)
            {
                tasks[t] = Task.Run(() =>
                {
                    var serializer = new SimpleBinarySerializer();
                    int localOk = 0;

                    for (int i = 0; i < LOOPS_PER_THREAD; ++i)
                    {
                        var u = new User { Age = i, Score = i * 1.23f, Name = "abc" + i };

                        var json = serializer.Serialize(u);
                        var x = serializer.Deserialize<User>(json);

                        if (x != null && x.Age == i && x.Name == ("abc" + i))
                            localOk++;

                    }

                    Interlocked.Increment(ref checkOk);
                });
            }

            Task.WaitAll(tasks);

            sw.Stop();
            Console.WriteLine(sw.Elapsed);
            Console.WriteLine($"{THREADS * LOOPS_PER_THREAD / sw.Elapsed.TotalSeconds / 10000:0.0000}w/s");
        }
    }

    public record class User
    {
        public int Age { get; set; }

        public float Score { get; set; }

        public string Name { get; set; }
    }
}
