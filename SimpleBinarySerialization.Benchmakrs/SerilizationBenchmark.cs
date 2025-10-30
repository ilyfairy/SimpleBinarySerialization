using BenchmarkDotNet.Attributes;
using MemoryPack;
using System.Text.Json.Serialization;

namespace SimpleBinarySerialization.Benchmakrs;

[MemoryDiagnoser]
public class SerilizationBenchmark
{
    private User obj = null!;
    public int Length { get; set; }

    private SimpleBinarySerializer serializer = new();

    [GlobalSetup]
    public void Setup()
    {
        obj = new User { Age = 30, Score = 99.9f, Name = "TestUser" };
    }

    [Benchmark]
    public object SimpleBinary()
    {
        return serializer.Deserialize<User>(serializer.Serialize(obj));
    }

    [Benchmark]
    public object TextJson()
    {
        return System.Text.Json.JsonSerializer.Deserialize<User>(System.Text.Json.JsonSerializer.Serialize(obj));
    }

    [Benchmark]
    public object TextJsonSG()
    {
        return System.Text.Json.JsonSerializer.Deserialize<User>(System.Text.Json.JsonSerializer.Serialize(obj, TextJsonGenerator.Default.User), TextJsonGenerator.Default.User);
    }

    [Benchmark]
    public object NewtonsoftJson()
    {
        return Newtonsoft.Json.JsonConvert.DeserializeObject<User>(Newtonsoft.Json.JsonConvert.SerializeObject(obj));
    }

    [Benchmark]
    public object MemoryPack()
    {
        var bytes = MemoryPackSerializer.Serialize<User>(obj);
        return MemoryPackSerializer.Deserialize<User>(bytes);
    }
}

[MemoryPackable]
public partial class User
{
    public int Age { get; set; }

    public float Score { get; set; }

    public string Name { get; set; }
}

[JsonSerializable(typeof(User))]
public partial class TextJsonGenerator : JsonSerializerContext
{

}