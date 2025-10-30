using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace SimpleBinarySerialization
{
    public class SimpleBinarySerializer
    {
        // 1byte start flag, 4byte uint length
        private const byte StringStart = 0x01;

        // 1byte boolean flag, 1byte value
        private const byte BooleanStart = 0x02;

        // 1byte object start, 4byte uint member count, members(key, value, key, value, ...)
        private const byte ObjectStart = 0x03;

        // 1byte array start, 4byte uint member count, members(item...)
        private const byte ArrayStart = 0x04;

        // 1byte struct, 2byte ushort length, struct bytes...
        private const byte StructStart = 0x05;

        // 1byte number flag, number bytes...
        private const byte NumberStart = 0x06;  // 0x06 - 0x06+10

        // 1byte null flag
        private const byte NullValue = 0xFF;

        private readonly Dictionary<Type, ConstructorInfo> _constructorCache = new Dictionary<Type, ConstructorInfo>();
        private readonly Dictionary<Type, PropertyInfo[]> _propertiesCache = new Dictionary<Type, PropertyInfo[]>();

        private static PropertyInfo[] GetSerializableProperties(Type type)
        {
            return type.GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(BinarySerializationInclude), false).Any())
                .ToArray();
        }

        private object CreateInstance(Type type)
        {
            if (!_constructorCache.TryGetValue(type, out var constructor))
            {
                constructor = type.GetConstructor(Type.EmptyTypes);
                if (constructor is null)
                {
                    throw new ArgumentException($"Type {type} does not have a parameterless constructor");
                }
                _constructorCache[type] = constructor;
            }

            return constructor.Invoke(null)!;
        }

        private unsafe void CoreSave(BinaryWriter stream, object? config)
        {
            if (config is null)
            {
                stream.Write((byte)NullValue);
                return;
            }
            else if (config is bool b)
            {
                stream.Write((byte)BooleanStart);
                stream.Write(b ? (byte)1 : (byte)0);
                return;
            }
            else if (config is string s)
            {
                var sBytes = Encoding.UTF8.GetBytes(s);
                stream.Write((byte)StringStart);
                stream.Write((uint)sBytes.Length);
                stream.Write(sBytes);
                return;
            }
            else if (config is IList list)
            {
                var count = list.Count;
                stream.Write((byte)ArrayStart);
                stream.Write((uint)count);
                for (int i = 0; i < count; i++)
                {
                    CoreSave(stream, list[i]);
                }
                return;
            }
            else if (config is IEnumerable enumerable)
            {
                List<object?> values = new List<object?>();
                foreach (var value in enumerable)
                {
                    values.Add(value);
                }

                stream.Write((byte)ArrayStart);
                stream.Write((uint)values.Count);
                foreach (var value in values)
                {
                    CoreSave(stream, value);
                }
                return;
            }
            else if (config is IDictionary dict)
            {
                var count = dict.Count;
                stream.Write((byte)ObjectStart);
                stream.Write((uint)count);
                foreach (DictionaryEntry entry in dict)
                {
                    CoreSave(stream, entry.Key);
                    CoreSave(stream, entry.Value);
                }
                return;
            }
            else if (config is byte nByte)
            {
                stream.Write((byte)(NumberStart));
                stream.Write(nByte);
                return;
            }
            else if (config is sbyte nSByte)
            {
                stream.Write((byte)(NumberStart + 1));
                stream.Write(nSByte);
                return;
            }
            else if (config is short nShort)
            {
                stream.Write((byte)(NumberStart + 2));
                stream.Write(nShort);
                return;
            }
            else if (config is ushort nUShort)
            {
                stream.Write((byte)(NumberStart + 3));
                stream.Write(nUShort);
                return;
            }
            else if (config is int nInt)
            {
                stream.Write((byte)(NumberStart + 4));
                stream.Write(nInt);
                return;
            }
            else if (config is uint nUInt)
            {
                stream.Write((byte)(NumberStart + 5));
                stream.Write(nUInt);
                return;
            }
            else if (config is long nLong)
            {
                stream.Write((byte)(NumberStart + 6));
                stream.Write(nLong);
                return;
            }
            else if (config is long nULong)
            {
                stream.Write((byte)(NumberStart + 7));
                stream.Write(nULong);
                return;
            }
            else if (config is float nFloat)
            {
                stream.Write((byte)(NumberStart + 8));
                stream.Write(nFloat);
                return;
            }
            else if (config is double nDouble)
            {
                stream.Write((byte)(NumberStart + 9));
                stream.Write(nDouble);
                return;
            }
            else if (config is decimal nDecimal)
            {
                stream.Write((byte)(NumberStart + 10));
                stream.Write(nDecimal);
                return;
            }
            else
            {
                var typeOfConfig = config.GetType();
                if (typeOfConfig.IsEnum)
                {
                    CoreSave(stream, config.ToString());
                    return;
                }
                else if (typeOfConfig.IsClass)
                {
                    if (!_propertiesCache.TryGetValue(typeOfConfig, out var properties))
                    {
                        _propertiesCache[typeOfConfig] = properties = GetSerializableProperties(typeOfConfig);
                    }

                    stream.Write((byte)ObjectStart);
                    stream.Write((uint)properties.Length);
                    foreach (var property in properties)
                    {
                        var value = property.GetValue(config);

                        CoreSave(stream, property.Name);
                        CoreSave(stream, value);
                    }

                    return;
                }
                else if (typeOfConfig.IsValueType)
                {
                    var structSize = Marshal.SizeOf(typeOfConfig);
                    if (structSize > ushort.MaxValue)
                    {
                        throw new ArgumentException("Structure is too large");
                    }

                    var buffer = new byte[structSize];
                    fixed (byte* ptr = buffer)
                    {
                        Marshal.StructureToPtr(config, (nint)ptr, false);
                    }

                    stream.Write((byte)StructStart);
                    stream.Write((ushort)structSize);
                    stream.Write(buffer);
                    return;
                }

                throw new ArgumentException($"Type not supported: {typeOfConfig}");
            }
        }

        private unsafe void CoreLoad(BinaryReader stream, ref object? config, Type? targetType)
        {
            if (config is not null &&
                targetType is null)
            {
                targetType = config.GetType();
            }

            var tag = stream.ReadByte();

            if (tag == StringStart)
            {
                var length = stream.ReadUInt32();
                var sBytes = stream.ReadBytes((int)length);
                var value = Encoding.UTF8.GetString(sBytes);

                if (targetType == typeof(string))
                {
                    config = value;
                    return;
                }
                else if (targetType is { IsEnum: true })
                {
                    config = Enum.Parse(targetType, value);
                    return;
                }

                throw new ArgumentException($"Invalid type for string value: {targetType}");
            }
            else if (tag == BooleanStart)
            {
                var bValue = stream.ReadByte();
                if (targetType == typeof(bool))
                {
                    config = bValue != 0;
                    return;
                }
                throw new ArgumentException($"Invalid type for boolean value: {targetType}");
            }
            else if (tag == ObjectStart)
            {
                if (config is null &&
                    targetType is not null)
                {
                    config = CreateInstance(targetType);
                }

                var count = stream.ReadUInt32();
                for (int i = 0; i < count; i++)
                {
                    var stringTag = stream.ReadByte();
                    if (stringTag != StringStart)
                    {
                        throw new FormatException("Invalid data");
                    }

                    var length = stream.ReadUInt32();
                    var sBytes = stream.ReadBytes((int)length);
                    var key = Encoding.UTF8.GetString(sBytes);

                    if (config is IDictionary dict)
                    {
                        object? currentValue = null;
                        if (dict.Contains(key))
                        {
                            currentValue = dict[key];
                        }

                        var newValue = currentValue;
                        CoreLoad(stream, ref newValue, null);
                        if (!ReferenceEquals(currentValue, newValue))
                        {
                            dict[key] = newValue;
                        }
                    }
                    else if (targetType is not null)
                    {
                        if (!_propertiesCache.TryGetValue(targetType, out var properties))
                        {
                            _propertiesCache[targetType] = properties = GetSerializableProperties(targetType);
                        }

                        var property = properties.FirstOrDefault(p => p.Name == key);
                        if (property is null)
                        {
                            // skip value
                            object? skipValue = null;
                            CoreLoad(stream, ref skipValue, null);
                            continue;
                        }

                        var currentValue = property.GetValue(config);
                        var newValue = currentValue;
                        CoreLoad(stream, ref newValue, property.PropertyType);
                        if (!ReferenceEquals(currentValue, newValue))
                        {
                            property.SetValue(config, newValue);
                        }
                    }
                }
            }
            else if (tag == ArrayStart)
            {
                var count = stream.ReadUInt32();

                if (config is null)
                {
                    List<object?> values = new List<object?>();
                    for (int i = 0; i < count; i++)
                    {
                        object? item = null;
                        CoreLoad(stream, ref item, null);
                        values.Add(item);
                    }

                    config = values;
                    return;
                }
                else if (config is IList list)
                {
                    var elementType = typeof(object);
                    if (targetType is not null)
                    {
                        if (targetType.IsArray)
                        {
                            elementType = targetType.GetElementType();
                        }
                    }

                    for (int i = 0; i < count; i++)
                    {
                        var currentElementType = elementType;

                        object? currentValue = null;
                        if (i < list.Count)
                        {
                            currentValue = list[i];
                        }

                        if (currentValue is not null)
                        {
                            currentElementType = currentValue.GetType();
                        }

                        var newValue = currentValue;

                        CoreLoad(stream, ref newValue, currentElementType);
                        if (i < list.Count &&
                            !ReferenceEquals(currentValue, newValue))
                        {
                            list[i] = newValue;
                        }
                    }

                    return;
                }
                else
                {
                    throw new ArgumentException($"Invalid type for array value: {targetType}");
                }
            }
            else if (tag == StructStart)
            {
                var structSize = stream.ReadUInt16();
                var structBytes = stream.ReadBytes(structSize);
                if (targetType is null)
                {
                    throw new ArgumentException("Target type is required for struct deserialization");
                }

                fixed (byte* ptr = structBytes)
                {
                    config = Marshal.PtrToStructure((nint)ptr, targetType);
                }

                return;
            }
            else if (tag == NumberStart + 0)
            {
                config = stream.ReadByte();
            }
            else if (tag == NumberStart + 1)
            {
                config = stream.ReadSByte();
            }
            else if (tag == NumberStart + 2)
            {
                config = stream.ReadInt16();
            }
            else if (tag == NumberStart + 3)
            {
                config = stream.ReadUInt16();
            }
            else if (tag == NumberStart + 4)
            {
                config = stream.ReadInt32();
            }
            else if (tag == NumberStart + 5)
            {
                config = stream.ReadUInt32();
            }
            else if (tag == NumberStart + 6)
            {
                config = stream.ReadInt64();
            }
            else if (tag == NumberStart + 7)
            {
                config = stream.ReadUInt64();
            }
            else if (tag == NumberStart + 8)
            {
                config = stream.ReadSingle();
            }
            else if (tag == NumberStart + 9)
            {
                config = stream.ReadDouble();
            }
            else if (tag == NumberStart + 10)
            {
                config = stream.ReadDecimal();
            }
            else if (tag == NullValue)
            {
                config = null;
            }
            else
            {
                throw new FormatException("Invalid data");
            }
        }

        public byte[] Serialize(object config)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            CoreSave(writer, config);
            writer.Flush();

            return ms.ToArray();
        }

        public T? Deserialize<T>(byte[] data)
        {

            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            object? value = default(T);
            CoreLoad(reader, ref value, typeof(T));
            if (value is T actualValue)
            {
                return actualValue;
            }

            return default;
        }

        public void Populate(object config, byte[] data)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);
            CoreLoad(reader, ref config, config.GetType());
        }
    }
}
