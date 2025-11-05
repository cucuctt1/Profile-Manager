using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace BasicDataBase.FileIO
{

    class FileWriter
    {
    public static string? schemaLine;
        public static BitArray decodedBits = new BitArray(1, false);
        // check for header format
        public static void ReadHeader(string MetaDataDir)
        {
            //file opening
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

        public void Write2DataFile(string MetadataDir, string DataDir, string schema, object[] Data)
        {
            ReadHeader(MetadataDir);
            //veryfied
            if (schemaLine != schema)
            {
                throw new ArgumentException("Schema mismatch");
            }
            Schema schemaStruct = Schema.FromString(schema);

            // opene data file
            var Dat = System.IO.File.OpenWrite(DataDir);
            //write mode append
            Dat.Seek(0, System.IO.SeekOrigin.End);
            using (var writer = new System.IO.BinaryWriter(Dat))
            {
                foreach (var item in Data)
                {
                    var bytes = DataTypeConverter.ObjectToBytes(item);
                    // write length-prefixed field
                    writer.Write(bytes.Length);
                    if (bytes.Length > 0)
                        writer.Write(bytes);
                }
                writer.Flush();
            }
        }

        public void Write2DataFile(string MetadataDir, string DataDir, string schema, object[,] Data)
        {
            ReadHeader(MetadataDir);
            //veryfied
            if (schemaLine != schema)
            {
                throw new ArgumentException("Schema mismatch");
            }
            Schema schemaStruct = Schema.FromString(schema);

            // open data file
            var Dat = System.IO.File.OpenWrite(DataDir);
            //write mode append
            Dat.Seek(0, System.IO.SeekOrigin.End);
            using (var writer = new System.IO.BinaryWriter(Dat))
            {
                for (int i = 0; i < Data.GetLength(0); i++)
                {
                    for (int j = 0; j < Data.GetLength(1); j++)
                    {
                        var bytes = DataTypeConverter.ObjectToBytes(Data[i, j]);
                        // write length-prefixed field
                        writer.Write(bytes.Length);
                        if (bytes.Length > 0)
                            writer.Write(bytes);
                    }
                    // recent write size can be inspected via Dat.Position if needed
                }
                writer.Flush();
            }
        }
        

        public void DeleteRecord(string dataPath, long recordStart, long recordEnd)
        {
            // Perform chunked copy from recordEnd -> recordStart, then truncate
            const int BufferSize = 81920; // 80 KB
            using (var fs = new FileStream(dataPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                long fileLength = fs.Length;

                if (recordStart < 0 || recordEnd < recordStart || recordEnd > fileLength)
                    throw new ArgumentOutOfRangeException("Invalid recordStart/recordEnd range");

                long bytesToMove = fileLength - recordEnd;
                if (bytesToMove > 0)
                {
                    byte[] buffer = new byte[BufferSize];
                    long readPos = recordEnd;
                    long writePos = recordStart;
                    long remaining = bytesToMove;

                    while (remaining > 0)
                    {
                        int toRead = (int)Math.Min(BufferSize, remaining);
                        fs.Seek(readPos, SeekOrigin.Begin);
                        int n = fs.Read(buffer, 0, toRead);
                        if (n <= 0) break;

                        fs.Seek(writePos, SeekOrigin.Begin);
                        fs.Write(buffer, 0, n);

                        readPos += n;
                        writePos += n;
                        remaining -= n;
                    }
                }

                // Truncate the file to remove leftover bytes
                long newLength = fileLength - (recordEnd - recordStart);
                fs.SetLength(newLength);
                fs.Flush();
            }
        }
    }
}