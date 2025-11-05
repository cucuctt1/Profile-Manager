using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BasicDataBase.FileIO;

namespace BasicDataBase.Table
{
    public record BlobReference(int RecordIndex, string Reference, string? AbsolutePath);
    public record OrphanBlob(string FileName, string AbsolutePath);

    public sealed class BlobHandler
    {
        private readonly TableManager _tableManager;

        public BlobHandler(TableManager tableManager)
        {
            _tableManager = tableManager ?? throw new ArgumentNullException(nameof(tableManager));
        }

        public string AddBlob(string tableName, int recordIndex, string fieldName, byte[] data)
        {
            return ReplaceBlob(tableName, recordIndex, fieldName, data);
        }

        public string AddBlobFromFile(string tableName, int recordIndex, string fieldName, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path required", nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("Source file not found", filePath);
            var bytes = File.ReadAllBytes(filePath);
            return ReplaceBlob(tableName, recordIndex, fieldName, bytes);
        }

        public string ReplaceBlob(string tableName, int recordIndex, string fieldName, byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var tableInfo = _tableManager.GetTableInfo(tableName);
            int fieldIndex = ResolveBlobFieldIndex(tableInfo, fieldName);

            var record = _tableManager.GetRecord(tableName, recordIndex) ?? throw new ArgumentOutOfRangeException(nameof(recordIndex), "Record not found");
            var newRecord = CloneRecord(record);
            newRecord[fieldIndex] = data;
            _tableManager.UpdateRecord(tableName, recordIndex, newRecord);

            var updated = _tableManager.GetRecord(tableName, recordIndex);
            if (updated != null && updated.Length > fieldIndex && updated[fieldIndex] is string path && !string.IsNullOrEmpty(path))
            {
                return path;
            }
            return string.Empty;
        }

        public void DeleteBlob(string tableName, int recordIndex, string fieldName)
        {
            var tableInfo = _tableManager.GetTableInfo(tableName);
            int fieldIndex = ResolveBlobFieldIndex(tableInfo, fieldName);
            var record = _tableManager.GetRecord(tableName, recordIndex) ?? throw new ArgumentOutOfRangeException(nameof(recordIndex), "Record not found");
            if (record.Length <= fieldIndex || record[fieldIndex] == null) return; // nothing to delete

            var newRecord = CloneRecord(record);
            newRecord[fieldIndex] = null!;
            _tableManager.UpdateRecord(tableName, recordIndex, newRecord);
        }

        public byte[]? ReadBlob(string tableName, int recordIndex, string fieldName)
        {
            return _tableManager.ReadBlob(tableName, recordIndex, fieldName);
        }

        public string? GetBlobPath(string tableName, int recordIndex, string fieldName)
        {
            return _tableManager.GetBlobPath(tableName, recordIndex, fieldName);
        }

        public IReadOnlyList<BlobReference> ListBlobs(string tableName, string fieldName)
        {
            var tableInfo = _tableManager.GetTableInfo(tableName);
            int fieldIndex = ResolveBlobFieldIndex(tableInfo, fieldName);
            var records = _tableManager.GetAllRecords(tableName);
            var list = new List<BlobReference>();
            for (int i = 0; i < records.Count; i++)
            {
                string? value = records[i].Length > fieldIndex ? records[i][fieldIndex] as string : null;
                if (!string.IsNullOrEmpty(value))
                {
                    list.Add(new BlobReference(i, value!, _tableManager.GetBlobPath(tableName, i, fieldName)));
                }
            }
            return list;
        }

        public IReadOnlyList<OrphanBlob> ListOrphanBlobs(string tableName)
        {
            var info = _tableManager.GetTableInfo(tableName);
            var blobDir = Path.Combine(info.Directory, "blobs");
            if (!Directory.Exists(blobDir)) return Array.Empty<OrphanBlob>();

            var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in info.Schema.Fields.Select((f, idx) => (f, idx)))
            {
                if (field.f.Type != FieldType.Blob) continue;
                var blobs = ListBlobs(tableName, field.f.Name);
                foreach (var blob in blobs)
                {
                    if (!string.IsNullOrEmpty(blob.Reference)) referenced.Add(blob.Reference);
                }
            }

            var orphans = new List<OrphanBlob>();
            foreach (var file in Directory.GetFiles(blobDir))
            {
                var name = Path.GetFileName(file);
                if (name != null && !referenced.Contains(name))
                {
                    orphans.Add(new OrphanBlob(name, file));
                }
            }
            return orphans;
        }

        private static object[] CloneRecord(object?[] record)
        {
            var clone = new object[record.Length];
            Array.Copy(record, clone, record.Length);
            return clone;
        }

        private static int ResolveBlobFieldIndex(TableInfo tableInfo, string fieldName)
        {
            for (int i = 0; i < tableInfo.Schema.Fields.Count; i++)
            {
                var field = tableInfo.Schema.Fields[i];
                if (field.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    if (field.Type != FieldType.Blob)
                        throw new ArgumentException($"Field '{fieldName}' is not a blob field");
                    return i;
                }
            }
            throw new ArgumentException($"Field '{fieldName}' not found in schema");
        }
    }
}
