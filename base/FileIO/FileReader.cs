// reader

using System;
using System.Collections;
using System.Collections.Generic;
using BasicDataBase.FileIO;

namespace BasicDataBase.FileIO
{
    public static class FileReader
    {
    public static string? schemaLine;
        public static BitArray decodedBits = new BitArray(1, false);

        // check for header format
        public static void ReadHeader(string MetaDataDir)
        {
            // file opening
            var Dat = System.IO.File.OpenRead(MetaDataDir);
            using (var reader = new System.IO.StreamReader(Dat))
            {
                // row 1  read schema
                schemaLine = reader.ReadLine();
                // row 2 read instruction
                string? instructionLine = reader.ReadLine();
                if (instructionLine == null) throw new InvalidDataException("Metadata instruction line missing");
                decodedBits = EDHex.HexToBit(instructionLine);
            }
        }

        public static object[,] ReadAllData(string MetaDataDir, string DataDir)
        {
            List<object[]> rows = new List<object[]>();
            ReadHeader(MetaDataDir);

            // parse schema so we can convert bytes to typed objects
            if (schemaLine == null) throw new InvalidDataException("Metadata schema line missing");
            Schema schema = Schema.FromString(schemaLine);

            // decoded bits to field num
            int fieldCount = 0;
            decodedBits = BitGenerator.BitTrim(decodedBits);
            for (int i = 0; i < decodedBits.Length; i++)
            {
                if (!decodedBits[i]) fieldCount++;
            }
            fieldCount++; // last field

            using (var reader = new System.IO.BinaryReader(System.IO.File.OpenRead(DataDir)))
            {
                // read records using known field count (length-prefixed fields)
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    List<object> currentRow = new List<object>();
                    int actualBytesRead = 0;
                    for (int fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++)
                    {
                        // if there's not enough bytes left to read a length, break
                        if (reader.BaseStream.Position + 4 > reader.BaseStream.Length)
                        {
                            break;
                        }
                        int len = reader.ReadInt32();
                        byte[] fieldBytes = Array.Empty<byte>();
                        if (len > 0)
                        {
                            fieldBytes = reader.ReadBytes(len);
                            //get actual bytes read
                            actualBytesRead += fieldBytes.Length;

                        }
                        

                        object? value = null;
                        if (fieldIndex < schema.Fields.Count)
                        {
                            var ftype = schema.Fields[fieldIndex].Type;
                            value = DataTypeConverter.BytesToObject(fieldBytes, ftype);
                        }
                        else
                        {
                            value = DataTypeConverter.BytesToString(fieldBytes);
                        }

                        currentRow.Add(value!);
                    }
                    if (currentRow.Count > 0)
                        rows.Add(currentRow.ToArray());
                }
            }
            if (rows.Count == 0)
                return new object[0, 0];

            int rowCount = rows.Count;
            int colCount = fieldCount;
            object[,] result = new object[rowCount, colCount];
            for (int i = 0; i < rowCount; i++)
                for (int j = 0; j < colCount; j++)
                    // rows[i][j] may be null; assign with null-forgiving to satisfy nullable analysis
                    result[i, j] = j < rows[i].Length ? rows[i][j] : null!;

            return result;
        }
    }
}