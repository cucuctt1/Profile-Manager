// generate bit sequence for schema instruction use offset padding

using System;
using System.Collections;
public static class BitGenerator
{
    public static BitArray GenerateBitPadding(int[] offset, int recordSize)
    {
        //bit length = record size + number of offsets -1 cell to nearest byte
        int bitLength = recordSize + offset.Length - 1;
        int actualBits = bitLength;
        // add 0 end padding round up to nearest byte
        if (bitLength % 8 != 0)
        {
            bitLength += 8 - (bitLength % 8);
        }
        BitArray bits = new BitArray(bitLength, true);
        // bit split by a 0
        if (offset.Length == 0) return bits;
        int pad = 0;
        for (int i = 1; i < offset.Length; i++)
        {
            // assign bit
            bits[offset[i] + pad] = false;
            pad++;
        }

        for (int i = actualBits; i < bitLength; i++)
        {
            bits[i] = false; // end padding bits set to 1
        }

        return bits;
    }

    
    public static BitArray BitTrim(BitArray bits)
    {
        int lastIndex = bits.Length - 1;
        for (int i = lastIndex; i >= 0; i--)
        {
            if (bits[i])
            {
                lastIndex = i;
                break;
            }
        }
        BitArray trimmedBits = new BitArray(lastIndex + 1);
        for (int i = 0; i <= lastIndex; i++)
        {
            trimmedBits[i] = bits[i];
        }
        return trimmedBits;
    }
    public static int[] DecodeBitPadding(BitArray bits)
    {
        // trim trailing false bits
        BitArray trimmedBits = BitTrim(bits);

        var offsets = new System.Collections.Generic.List<int>();
        offsets.Add(0); // first offset is always 0
        for (int i = 0; i < trimmedBits.Count; i++)
        {
            if (!trimmedBits[i])
            {
                offsets.Add(i - offsets.Count + 1);
            }
        }
        return offsets.ToArray();
    }
    //helper

    public static string BitArrayToString(BitArray bits)
    {
        char[] chars = new char[bits.Count];
        for (int i = 0; i < bits.Count; i++)
        {
            chars[i] = bits[i] ? '1' : '0';
        }
        
        
        return new string(chars);
    }
}