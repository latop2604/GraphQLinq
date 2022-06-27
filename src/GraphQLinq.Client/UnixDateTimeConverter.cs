using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GraphQLinq.Client
{
    public class UnixDateTimeConverter : JsonConverter<DateTime?>
    {
        internal static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            bool flag = IsNullable(typeToConvert);
            if (reader.TokenType == JsonTokenType.Null)
            {
                if (!flag)
                {
                    throw new JsonException(string.Format(CultureInfo.InvariantCulture, "Cannot convert null value to {0}.", typeToConvert));
                }
                return default;
            }

            long result;
            if (reader.TokenType == JsonTokenType.Number)
            {
                result = reader.GetInt64();
            }
            else
            {
                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException(string.Format(CultureInfo.InvariantCulture, "Unexpected token parsing date. Expected Integer or String, got {0}.", reader.TokenType));
                }

                string s = reader.GetString();
                if (!long.TryParse(s, out result))
                {
                    var d = DateTime.Parse(s);
                    return d;
                    throw new JsonException(string.Format(CultureInfo.InvariantCulture, "Cannot convert invalid value to {0}.", typeToConvert));
                }
            }

            if (result >= 0)
            {
                DateTime unixEpoch = UnixEpoch;
                DateTime dateTime = unixEpoch.AddSeconds(result);
                return dateTime;
            }
            throw new JsonException(string.Format(CultureInfo.InvariantCulture, "Cannot convert value that is before Unix epoch of 00:00:00 UTC on 1 January 1970 to {0}.", typeToConvert));

        }
        private static bool IsNullableType(Type t)
        {
            if (t.IsGenericType)
            {
                return t.GetGenericTypeDefinition() == typeof(Nullable<>);
            }
            return false;
        }

        private static bool IsNullable(Type t)
        {
            if (t.IsValueType)
            {
                return IsNullableType(t);
            }
            return true;
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
