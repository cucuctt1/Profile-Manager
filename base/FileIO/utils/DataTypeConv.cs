using System;
using System.Text;

namespace BasicDataBase.FileIO
{
    public static class DataTypeConverter
    {
        public static byte[] ObjectToBytes(object? obj)
        {
            if (obj == null) return Array.Empty<byte>();
            switch (obj)
            {
                case byte b:
                    return new[] { b };
                case byte[] ba:
                    return ba;
                case short s:
                    return BitConverter.GetBytes(s);
                case ushort us:
                    return BitConverter.GetBytes(us);
                case int i:
                    return BitConverter.GetBytes(i);
                case uint ui:
                    return BitConverter.GetBytes(ui);
                case long l:
                    return BitConverter.GetBytes(l);
                case ulong ul:
                    return BitConverter.GetBytes(ul);
                case float f:
                    return BitConverter.GetBytes(f);
                case double d:
                    return BitConverter.GetBytes(d);
                case bool bo:
                    return BitConverter.GetBytes(bo);
                case char c:
                    return BitConverter.GetBytes(c);
                case string str:
                    return Encoding.UTF8.GetBytes(str ?? string.Empty);
                case DateTime dt:
                    return BitConverter.GetBytes(dt.Ticks);
                default:
                    // fallback: serialize via ToString()
                    var text = obj.ToString();
                    return text == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(text);
            }
        }

        // Convert bytes (from data file) back into a typed object using the schema's field type
        public static object? BytesToObject(byte[]? bytes, FieldType type)
        {
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                return type switch
                {
                    FieldType.Integer => BitConverter.ToInt32(bytes, 0),
                    FieldType.Boolean => BitConverter.ToBoolean(bytes, 0),
                    FieldType.DateTime => new DateTime(BitConverter.ToInt64(bytes, 0)),
                    FieldType.Blob => Encoding.UTF8.GetString(bytes), // treat blob as UTF8 string (path or text)
                    FieldType.String => Encoding.UTF8.GetString(bytes),
                    _ => Encoding.UTF8.GetString(bytes)
                };
            }
            catch
            {
                // On error, fall back to raw string
                return Encoding.UTF8.GetString(bytes);
            }
        }

        // simple helper to get a string from bytes when schema isn't available
        public static string? BytesToString(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            return Encoding.UTF8.GetString(bytes);
        }
    }
}