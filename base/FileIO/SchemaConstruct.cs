using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;


namespace BasicDataBase.FileIO
{
    // SchemaConstruct represents the schema of a table: column names and types
    // datatype construct
    // add blob
    public enum FieldType
    {
        Integer,
        String,
        Boolean,
        DateTime,
        Blob
    }

    public class Field
    {
        public string Name { get; set; }
        public FieldType Type { get; set; }
        public int MaxLength { get; set; }

        public Field(string name, FieldType type, int maxLength = 0)
        {
            Name = name;
            Type = type;
            MaxLength = maxLength;
        }
    }
    public class Schema
    {
        public List<Field> Fields { get; private set; } = new List<Field>();

        public void AddField(string name, FieldType type, int maxLength = 0)
        {
            Fields.Add(new Field(name, type, maxLength));
        }
        public static Schema FromString(string schemaString)
        {
            var schema = new Schema();
            var fieldDefs = schemaString.Split(',');
            foreach (var def in fieldDefs)
            {
                var parts = def.Split(':');
                string name = parts[0];
                string typeStr = parts[1];
                int maxLength = 0;
                if (parts.Length > 2)
                {
                    int.TryParse(parts[2], out maxLength);
                }
                FieldType type = typeStr.ToLower() switch
                {
                    "int" => FieldType.Integer,
                    "string" => FieldType.String,
                    "bool" => FieldType.Boolean,
                    "datetime" => FieldType.DateTime,
                    "blob" => FieldType.Blob,
                    _ => throw new ArgumentException($"Unknown field type: {typeStr}")
                };
                schema.AddField(name, type, maxLength);
            }

            return schema;
        }

        public static Schema FromArray(string[] fields)
        {
            var schemaString = string.Join(",", fields);
            return FromString(schemaString);
        }

        // get schema as string
        public override string ToString()
        {
            List<string> fieldStrs = new List<string>();
            foreach (var field in Fields)
            {
                string typeStr = field.Type switch
                {
                    FieldType.Integer => "int",
                    FieldType.String => "string",
                    FieldType.Boolean => "bool",
                    FieldType.DateTime => "datetime",
                    FieldType.Blob => "blob",       
                    _ => throw new ArgumentException($"Unknown field type: {field.Type}")
                };
                if (field.Type == FieldType.String && field.MaxLength > 0)
                {
                    fieldStrs.Add($"{field.Name}:{typeStr}:{field.MaxLength}");
                }
                else
                {
                    fieldStrs.Add($"{field.Name}:{typeStr}");
                }
            }
            return string.Join(",", fieldStrs);
        }
    }

    // schema build functions -> schema data
}

//////////////// example usage ///////////////////////
// Schema schema = Schema.FromString("Id:int,Name:string:100,IsActive:bool,CreatedAt:datetime");
// Schema schema = Schema.FromArray(new string[] { "Id:int", "Name:string:100", "IsActive:bool", "CreatedAt:datetime" });
// schema.Fields -> list of Field objects with Name, Type, MaxLength
// string schemaStr = schema.ToString(); // "Id:int,Name:string:100,IsActive:bool,CreatedAt:datetime"