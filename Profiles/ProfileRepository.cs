using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BasicDataBase.Table;

namespace ProfileManager.Profiles
{
    public sealed class ProfileRepository
    {
        private const string DefaultTableName = "Profiles";
        private const string SchemaString = "id:int,hovaten:string:256,namsinh:datetime,noisinh:string:256,quequan:string:256,lop:string:64,tongiao:string:64,gioitinh:string:32,nienkhoa:string:64,masobhyt:string:64,masobhxh:string:64,diachi:string:256,sdt:string:32,vaodoan:bool,vaodang:bool";
        private readonly TableManager tableManager;
        private readonly HashSet<string> indexedFields = new(StringComparer.OrdinalIgnoreCase);
        private readonly string tableName;

        public ProfileRepository(string? rootDirectory = null, string? customTableName = null)
        {
            tableManager = new TableManager(rootDirectory);
            tableName = string.IsNullOrWhiteSpace(customTableName) ? DefaultTableName : customTableName;
            EnsureTable();
        }

        public IReadOnlyList<ProfileRow> GetAll()
        {
            var rows = tableManager.GetAllRecords(tableName);
            var list = new List<ProfileRow>();
            for (var i = 0; i < rows.Count; i++)
            {
                var record = rows[i];
                list.Add(new ProfileRow(i, ToRecord(record)));
            }
            return list;
        }

        public ProfileRow? GetByIndex(int index)
        {
            var record = tableManager.GetRecord(tableName, index);
            if (record == null) return null;
            return new ProfileRow(index, ToRecord(record));
        }

        public IReadOnlyList<ProfileRow> Search(string fieldName, string value, bool exact)
        {
            var query = value.Trim();
            if (string.IsNullOrWhiteSpace(query)) return GetAll();
            var field = NormalizeField(fieldName);
            if (!exact)
            {
                return SearchPartial(field, query);
            }
            if (field.Equals("namsinh", StringComparison.OrdinalIgnoreCase))
            {
                if (!DateTime.TryParse(query, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var parsed))
                {
                    if (!DateTime.TryParse(query, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed)) return Array.Empty<ProfileRow>();
                    parsed = DateTime.SpecifyKind(parsed, DateTimeKind.Utc).ToLocalTime();
                }
                var day = parsed.Date;
                return FilterByDate(day, day);
            }
            EnsureIndex(field);
            var key = query;
            if (field.Equals("id", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(query, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return Array.Empty<ProfileRow>();
                key = parsed.ToString(CultureInfo.InvariantCulture);
            }
            if (field.Equals("vaodoan", StringComparison.OrdinalIgnoreCase) || field.Equals("vaodang", StringComparison.OrdinalIgnoreCase))
            {
                if (!bool.TryParse(query, out var parsedBool)) return Array.Empty<ProfileRow>();
                key = parsedBool.ToString();
            }
            var indexes = tableManager.SearchExact(tableName, field, key);
            var list = new List<ProfileRow>();
            foreach (var idx in indexes)
            {
                var record = tableManager.GetRecord(tableName, idx);
                if (record == null) continue;
                list.Add(new ProfileRow(idx, ToRecord(record)));
            }
            return list;
        }

        private IReadOnlyList<ProfileRow> SearchPartial(string field, string value)
        {
            var rows = GetAll();
            var list = new List<ProfileRow>();
            foreach (var row in rows)
            {
                var text = GetFieldText(row.Record, field);
                if (text.Contains(value, StringComparison.OrdinalIgnoreCase)) list.Add(row);
            }
            return list;
        }

        private static string GetFieldText(ProfileRecord record, string field)
        {
            return field switch
            {
                "id" => record.Id.ToString(CultureInfo.InvariantCulture),
                "hovaten" => record.HoVaTen ?? string.Empty,
                "namsinh" => BuildDateText(record.NamSinh),
                "noisinh" => record.NoiSinh ?? string.Empty,
                "quequan" => record.QueQuan ?? string.Empty,
                "lop" => record.Lop ?? string.Empty,
                "tongiao" => record.TonGiao ?? string.Empty,
                "gioitinh" => record.GioiTinh ?? string.Empty,
                "nienkhoa" => record.NienKhoa ?? string.Empty,
                "masobhyt" => record.MaSoBhyt ?? string.Empty,
                "masobhxh" => record.MaSoBhxh ?? string.Empty,
                "diachi" => record.DiaChi ?? string.Empty,
                "sdt" => record.Sdt ?? string.Empty,
                "vaodoan" => record.VaoDoan ? "true yes 1" : "false no 0",
                "vaodang" => record.VaoDang ? "true yes 1" : "false no 0",
                _ => record.HoVaTen ?? string.Empty
            };
        }

        private static string BuildDateText(DateTime date)
        {
            if (date == default) return string.Empty;
            var invariant = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var current = date.ToString(CultureInfo.CurrentCulture);
            return invariant + " " + current;
        }

        public IReadOnlyList<ProfileRow> FilterByDate(DateTime? from, DateTime? to)
        {
            var rows = GetAll();
            var list = new List<ProfileRow>();
            foreach (var row in rows)
            {
                var date = row.Record.NamSinh;
                var include = true;
                if (from.HasValue && date < from.Value.Date) include = false;
                if (to.HasValue && date > to.Value.Date.AddDays(1).AddTicks(-1)) include = false;
                if (include) list.Add(row);
            }
            return list;
        }

        public void Add(ProfileRecord record)
        {
            var values = ToValues(record);
            tableManager.InsertRecord(tableName, values);
        }

        public void Update(int index, ProfileRecord record)
        {
            var values = ToValues(record);
            tableManager.UpdateRecord(tableName, index, values);
        }

        public void Delete(int index)
        {
            tableManager.DeleteRecord(tableName, index);
        }

        public int GetNextId()
        {
            var all = GetAll();
            var max = 0;
            foreach (var row in all)
            {
                if (row.Record.Id > max) max = row.Record.Id;
            }
            return max + 1;
        }

        public void WipeAll()
        {
            tableManager.DropTable(tableName);
            var schema = BasicDataBase.FileIO.Schema.FromString(SchemaString);
            tableManager.CreateTable(tableName, schema);
            indexedFields.Clear();
        }

        private void EnsureTable()
        {
            try
            {
                var schema = BasicDataBase.FileIO.Schema.FromString(SchemaString);
                tableManager.CreateTable(tableName, schema);
            }
            catch (InvalidOperationException)
            {
            }
            catch (IOException)
            {
            }
            try
            {
                var info = tableManager.GetTableInfo(tableName);
                if (!string.Equals(info.Schema.ToString(), SchemaString, StringComparison.OrdinalIgnoreCase))
                {
                    var schema = BasicDataBase.FileIO.Schema.FromString(SchemaString);
                    tableManager.DropTable(tableName);
                    tableManager.CreateTable(tableName, schema);
                    indexedFields.Clear();
                }
            }
            catch
            {
            }
        }

        private void EnsureIndex(string fieldName)
        {
            if (fieldName.Equals("namsinh", StringComparison.OrdinalIgnoreCase)) return;
            if (indexedFields.Contains(fieldName)) return;
            tableManager.BuildIndex(tableName, fieldName, false);
            indexedFields.Add(fieldName);
        }

        private static ProfileRecord ToRecord(object?[] values)
        {
            var record = new ProfileRecord();
            if (values.Length > 0 && values[0] is int id) record.Id = id;
            if (values.Length > 1) record.HoVaTen = values[1]?.ToString();
            if (values.Length > 2 && values[2] is DateTime dob) record.NamSinh = dob;
            if (values.Length > 3) record.NoiSinh = values[3]?.ToString();
            if (values.Length > 4) record.QueQuan = values[4]?.ToString();
            if (values.Length > 5) record.Lop = values[5]?.ToString();
            if (values.Length > 6) record.TonGiao = values[6]?.ToString();
            if (values.Length > 7) record.GioiTinh = values[7]?.ToString();
            if (values.Length > 8) record.NienKhoa = values[8]?.ToString();
            if (values.Length > 9) record.MaSoBhyt = values[9]?.ToString();
            if (values.Length > 10) record.MaSoBhxh = values[10]?.ToString();
            if (values.Length > 11) record.DiaChi = values[11]?.ToString();
            if (values.Length > 12) record.Sdt = values[12]?.ToString();
            if (values.Length > 13 && values[13] is bool vaoDoan) record.VaoDoan = vaoDoan;
            if (values.Length > 14 && values[14] is bool vaoDang) record.VaoDang = vaoDang;
            return record;
        }

        private static object[] ToValues(ProfileRecord record)
        {
            return new object[]
            {
                record.Id,
                record.HoVaTen ?? string.Empty,
                record.NamSinh == default ? DateTime.UtcNow : record.NamSinh,
                record.NoiSinh ?? string.Empty,
                record.QueQuan ?? string.Empty,
                record.Lop ?? string.Empty,
                record.TonGiao ?? string.Empty,
                record.GioiTinh ?? string.Empty,
                record.NienKhoa ?? string.Empty,
                record.MaSoBhyt ?? string.Empty,
                record.MaSoBhxh ?? string.Empty,
                record.DiaChi ?? string.Empty,
                record.Sdt ?? string.Empty,
                record.VaoDoan,
                record.VaoDang
            };
        }

        private static string NormalizeField(string field)
        {
            return field switch
            {
                "Id" or "id" => "id",
                "HoVaTen" or "hovaten" => "hovaten",
                "NamSinh" or "namsinh" => "namsinh",
                "NoiSinh" or "noisinh" => "noisinh",
                "QueQuan" or "quequan" => "quequan",
                "Lop" or "lop" => "lop",
                "TonGiao" or "tongiao" => "tongiao",
                "GioiTinh" or "gioitinh" => "gioitinh",
                "NienKhoa" or "nienkhoa" => "nienkhoa",
                "MaSoBhyt" or "masobhyt" => "masobhyt",
                "MaSoBhxh" or "masobhxh" => "masobhxh",
                "DiaChi" or "diachi" => "diachi",
                "Sdt" or "sdt" => "sdt",
                "VaoDoan" or "vaodoan" => "vaodoan",
                "VaoDang" or "vaodang" => "vaodang",
                _ => "hovaten"
            };
        }
    }

    public readonly record struct ProfileRow(int Index, ProfileRecord Record);
}
