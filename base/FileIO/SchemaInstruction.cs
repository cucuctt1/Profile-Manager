// file reading and writing guider (data schema instruction)
// defines schema structure for table data files
// make byte range reading/writing space 
// guided by schema instructions


using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using BasicDataBase.FileIO;

public class SchemaInstruction
{
    public Schema Schema { get; private set; }
    public List<int> FieldOffsets { get; private set; } = new List<int>();
    public int RecordSize { get; private set; }

    public BitArray ByteRule { get; private set; }
    public SchemaInstruction(Schema schema)
    {
        Schema = schema;
        CalculateOffsetsAndSize();
        ByteRule = BitGenerator.GenerateBitPadding(FieldOffsets.ToArray(), RecordSize);
    }

    private void CalculateOffsetsAndSize()
    {
        int offset = 0;
        foreach (var field in Schema.Fields)
        {
            FieldOffsets.Add(offset);
            offset += GetFieldSize(field);
        }
        RecordSize = offset;
    }

    private int GetFieldSize(Field field)
    {
        return field.Type switch
        {
            FieldType.Integer => sizeof(int),
            FieldType.Boolean => sizeof(bool),
            FieldType.DateTime => sizeof(long), // store DateTime as ticks (long)
            FieldType.Blob => 128 * sizeof(char), // store Blob as fixed-length string of 128 characters
            FieldType.String => field.MaxLength > 0 ? field.MaxLength * sizeof(char) : 50 * sizeof(char), // default max length 50
            
            _ => throw new Exception("Unknown field type")
        };
    }
}