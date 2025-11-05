using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BasicDataBase.FileIO
{
    // High level file IO manager that exposes read/write/edit/delete operations
    // using the project's length-prefixed field format and metadata.
    public static class FileIOManager
    {
        // Read schema and compute field count from metadata
        public static (Schema schema, int fieldCount) LoadSchemaAndFieldCount(string metadataPath)
        {
            // reuse FileReader.ReadHeader behaviour
            using (var reader = new StreamReader(File.OpenRead(metadataPath)))
            {
                string? schemaLine = reader.ReadLine();
                string? instructionLine = reader.ReadLine();
                if (schemaLine == null) throw new InvalidDataException("Metadata schema line missing");
                if (instructionLine == null) throw new InvalidDataException("Metadata instruction line missing");
                var bits = EDHex.HexToBit(instructionLine);
                bits = BitGenerator.BitTrim(bits);
                int fieldCount = 0;
                for (int i = 0; i < bits.Length; i++) if (!bits[i]) fieldCount++;
                fieldCount++; // last field
                var schema = Schema.FromString(schemaLine);
                return (schema, fieldCount);
            }
        }

        // Append a single record (array of field objects) to data file
        public static void AppendRecord(string metadataPath, string dataPath, object?[] record)
        {
            var (schema, fieldCount) = LoadSchemaAndFieldCount(metadataPath);
            using (var fs = File.Open(dataPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
            using (var writer = new BinaryWriter(fs))
            {
                fs.Seek(0, SeekOrigin.End);
                for (int i = 0; i < record.Length; i++)
                {
                    var bytes = DataTypeConverter.ObjectToBytes(record[i]);
                    writer.Write(bytes.Length);
                    if (bytes.Length > 0)
                        writer.Write(bytes);
                }
                writer.Flush();
            }
        }

        // Read the byte offsets (start inclusive, end exclusive) of a record by index
        // Returns true if found; out start/end are file offsets
        public static bool TryGetRecordOffsets(string dataPath, int fieldCount, long recordIndex, out long startOffset, out long endOffset)
        {
            startOffset = 0; endOffset = 0;
            using (var fs = File.OpenRead(dataPath))
            using (var reader = new BinaryReader(fs))
            {
                long currentIndex = 0;
                while (fs.Position < fs.Length)
                {
                    long recordStartPos = fs.Position;
                    bool truncated = false;
                    for (int f = 0; f < fieldCount; f++)
                    {
                        if (fs.Position + 4 > fs.Length) { truncated = true; break; }
                        int len = reader.ReadInt32();
                        if (len < 0) len = 0;
                        if (fs.Position + len > fs.Length) { truncated = true; break; }
                        fs.Seek(len, SeekOrigin.Current);
                    }
                    if (truncated) break;
                    long recordEndPos = fs.Position;
                    if (currentIndex == recordIndex)
                    {
                        startOffset = recordStartPos;
                        endOffset = recordEndPos;
                        return true;
                    }
                    currentIndex++;
                }
            }
            return false;
        }

        // Read a record by index
        public static object?[]? ReadRecord(string metadataPath, string dataPath, long recordIndex)
        {
            var (schema, fieldCount) = LoadSchemaAndFieldCount(metadataPath);
            if (!TryGetRecordOffsets(dataPath, fieldCount, (int)recordIndex, out var s, out var e))
                return null;

            using (var fs = File.OpenRead(dataPath))
            using (var reader = new BinaryReader(fs))
            {
                fs.Seek(s, SeekOrigin.Begin);
                var values = new List<object?>();
                for (int i = 0; i < fieldCount; i++)
                {
                    if (fs.Position + 4 > fs.Length) break;
                    int len = reader.ReadInt32();
                    byte[] data = Array.Empty<byte>();
                    if (len > 0) data = reader.ReadBytes(len);
                    object? val = null;
                    if (i < schema.Fields.Count)
                        val = DataTypeConverter.BytesToObject(data, schema.Fields[i].Type);
                    else
                        val = DataTypeConverter.BytesToString(data);
                    values.Add(val);
                }
                return values.ToArray();
            }
        }

        // Read all records into a 2D array (rows x cols)
        public static object?[,] ReadAll(string metadataPath, string dataPath)
        {
            var (schema, fieldCount) = LoadSchemaAndFieldCount(metadataPath);
            var rows = new List<object?[]>();
            using (var fs = File.OpenRead(dataPath))
            using (var reader = new BinaryReader(fs))
            {
                while (fs.Position < fs.Length)
                {
                    var values = new List<object?>();
                    bool truncated = false;
                    for (int i = 0; i < fieldCount; i++)
                    {
                        if (fs.Position + 4 > fs.Length) { truncated = true; break; }
                        int len = reader.ReadInt32();
                        if (len < 0) len = 0;
                        if (fs.Position + len > fs.Length) { truncated = true; break; }
                        byte[] data = reader.ReadBytes(len);
                        object? val = null;
                        if (i < schema.Fields.Count)
                            val = DataTypeConverter.BytesToObject(data, schema.Fields[i].Type);
                        else
                            val = DataTypeConverter.BytesToString(data);
                        values.Add(val);
                    }
                    if (truncated) break;
                    rows.Add(values.ToArray());
                }
            }

            if (rows.Count == 0) return new object?[0, 0];
            var result = new object?[rows.Count, fieldCount];
            for (int r = 0; r < rows.Count; r++)
                for (int c = 0; c < fieldCount; c++)
                    result[r, c] = c < rows[r].Length ? rows[r][c] : null;
            return result;
        }

        // Edit a record by index: append the new record and delete the old one (compaction by shifting)
        public static void EditRecord(string metadataPath, string dataPath, long recordIndex, object?[] newRecord)
        {
            // Append new record
            AppendRecord(metadataPath, dataPath, newRecord);

            // Delete old record bytes by locating offsets
            var (schema, fieldCount) = LoadSchemaAndFieldCount(metadataPath);
            if (TryGetRecordOffsets(dataPath, fieldCount, (int)recordIndex, out var s, out var e))
            {
                // Use existing FileWriter.DeleteRecord which does chunked shift
                var writer = new FileWriter();
                writer.DeleteRecord(dataPath, s, e);
            }
        }

        // Delete a record by index (compacts file)
        public static void DeleteRecordByIndex(string metadataPath, string dataPath, long recordIndex)
        {
            var (schema, fieldCount) = LoadSchemaAndFieldCount(metadataPath);
            if (TryGetRecordOffsets(dataPath, fieldCount, (int)recordIndex, out var s, out var e))
            {
                var writer = new FileWriter();
                writer.DeleteRecord(dataPath, s, e);
            }
        }
    }
}
