using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
//utils loading


namespace BasicDataBase.FileIO
{
    public class MetaWriter
    {
        public static void WriteMetaData(string MetaDataDir, Schema schema, BitArray bits)
        {
            //write in bytes mode

            var Dat = System.IO.File.OpenWrite(MetaDataDir);
            using (var writer = new System.IO.StreamWriter(Dat))
            {
                // row 1 write schema
                writer.WriteLine(schema.ToString());
                // row 2 write instruction
                string encodedbits = EDHex.BitToHex(bits);
                writer.WriteLine(encodedbits);
            }
        }


    }
}