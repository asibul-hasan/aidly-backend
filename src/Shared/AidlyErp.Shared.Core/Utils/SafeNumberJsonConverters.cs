using System.Text.Json;
using System.Text.Json.Serialization;

namespace AidlyErp.Shared.Core.Utils;

public class SafeLongJsonConverter : JsonConverter<long>
{
    public override bool HandleNull => true;

    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return 0;
        }
        if (reader.TokenType == JsonTokenType.String)
        {
            string? str = reader.GetString();
            if (string.IsNullOrWhiteSpace(str)) return 0;
            if (long.TryParse(str, out long val)) return val;
            return 0;
        }
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt64();
        }
        return 0;
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

public class SafeIntJsonConverter : JsonConverter<int>
{
    public override bool HandleNull => true;

    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return 0;
        }
        if (reader.TokenType == JsonTokenType.String)
        {
            string? str = reader.GetString();
            if (string.IsNullOrWhiteSpace(str)) return 0;
            if (int.TryParse(str, out int val)) return val;
            return 0;
        }
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32();
        }
        return 0;
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

public class SafeShortJsonConverter : JsonConverter<short>
{
    public override bool HandleNull => true;

    public override short Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return 0;
        }
        if (reader.TokenType == JsonTokenType.String)
        {
            string? str = reader.GetString();
            if (string.IsNullOrWhiteSpace(str)) return 0;
            if (short.TryParse(str, out short val)) return val;
            return 0;
        }
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt16();
        }
        return 0;
    }

    public override void Write(Utf8JsonWriter writer, short value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

public class SafeDecimalJsonConverter : JsonConverter<decimal>
{
    public override bool HandleNull => true;

    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return 0m;
        }
        if (reader.TokenType == JsonTokenType.String)
        {
            string? str = reader.GetString();
            if (string.IsNullOrWhiteSpace(str)) return 0m;
            if (decimal.TryParse(str, out decimal val)) return val;
            return 0m;
        }
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }
        return 0m;
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}
