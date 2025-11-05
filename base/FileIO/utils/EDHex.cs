using System;
using System.Collections;
using System.Text;
class EDHex 
{
public static string BitToHex(BitArray bits)
{
    int numBytes = (bits.Length + 7) / 8;
    byte[] bytes = new byte[numBytes];

    for (int i = 0; i < bits.Length; i++)
    {
        if (bits[i])
            bytes[i / 8] |= (byte)(1 << (7 - (i % 8))); // MSB first
    }

    StringBuilder sb = new StringBuilder(numBytes * 2);
    foreach (byte b in bytes)
        sb.AppendFormat("{0:X2}", b); // 2-digit uppercase hex

    return sb.ToString(); // e.g., "4F2A01"

    // Convert those bytes into ASCII characters
   
}
    public static BitArray HexToBit(string input)
    {
        int numBytes = input.Length / 2;
        byte[] bytes = new byte[numBytes];

        for (int i = 0; i < numBytes; i++)
        {
            bytes[i] = Convert.ToByte(input.Substring(i * 2, 2), 16);
        }

        BitArray bits = new BitArray(numBytes * 8);
        for (int i = 0; i < bits.Length; i++)
        {
            bits[i] = (bytes[i / 8] & (1 << (7 - (i % 8)))) != 0; // MSB first
        }

        return bits;
    }
}