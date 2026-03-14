using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class FftMultiplier : IMultiplier
{
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        var da = a.GetDigits();
        var db = b.GetDigits();

        if (da.Length == 1 && da[0] == 0) return new BetterBigInteger(new uint[] { 0 });
        if (db.Length == 1 && db[0] == 0) return new BetterBigInteger(new uint[] { 0 });

        ushort[] aChunks = ToUshortChunks(da);
        ushort[] bChunks = ToUshortChunks(db);

        int maxChunks = aChunks.Length + bChunks.Length;
        int n = 1;
        while (n < maxChunks) n <<= 1;

        Complex[] ca = new Complex[n];
        for (int i = 0; i < aChunks.Length; i++) ca[i] = new Complex(aChunks[i], 0);

        Complex[] cb = new Complex[n];
        for (int i = 0; i < bChunks.Length; i++) cb[i] = new Complex(bChunks[i], 0);

        Fft(ca, false);
        Fft(cb, false);

        for (int i = 0; i < n; i++)
        {
            ca[i] = ca[i] * cb[i];
        }

        Fft(ca, true);

        ulong[] resChunks = new ulong[n];
        for (int i = 0; i < n; i++)
        {
            resChunks[i] = (ulong)Math.Round(ca[i].Real);
        }

        return FromChunks(resChunks, a.IsNegative != b.IsNegative);
    }

    private static ushort[] ToUshortChunks(ReadOnlySpan<uint> digits)
    {
        ushort[] chunks = new ushort[digits.Length * 2];
        for (int i = 0; i < digits.Length; i++)
        {
            chunks[i * 2] = (ushort)(digits[i] & 0xFFFF);
            chunks[i * 2 + 1] = (ushort)(digits[i] >> 16);
        }
        return chunks;
    }

    private static void Fft(Complex[] a, bool invert)
    {
        int n = a.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
                j ^= bit;
            j ^= bit;

            if (i < j)
            {
                var temp = a[i];
                a[i] = a[j];
                a[j] = temp;
            }
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = 2 * Math.PI / len * (invert ? -1 : 1);
            Complex wlen = new Complex(Math.Cos(angle), Math.Sin(angle));
            for (int i = 0; i < n; i += len)
            {
                Complex w = new Complex(1, 0);
                for (int j = 0; j < len / 2; j++)
                {
                    Complex u = a[i + j];
                    Complex v = a[i + j + len / 2] * w;
                    a[i + j] = u + v;
                    a[i + j + len / 2] = u - v;
                    w *= wlen;
                }
            }
        }

        if (invert)
        {
            for (int i = 0; i < n; i++)
                a[i] /= n;
        }
    }

    private static BetterBigInteger FromChunks(ulong[] chunks, bool isNegative)
    {
        ulong carry = 0;
        int maxIndex = -1;
        for (int i = 0; i < chunks.Length; i++)
        {
            ulong val = chunks[i] + carry;
            chunks[i] = val & 0xFFFF;
            carry = val >> 16;
            if (chunks[i] != 0) maxIndex = i;
        }

        while (carry > 0)
        {
            maxIndex++;
            if (maxIndex < chunks.Length)
            {
                chunks[maxIndex] = carry & 0xFFFF;
            }
            else
            {
                Array.Resize(ref chunks, chunks.Length + 1);
                chunks[chunks.Length - 1] = carry & 0xFFFF;
            }
            carry >>= 16;
        }

        if (maxIndex < 0) return new BetterBigInteger(new uint[] { 0 });

        int uintLen = (maxIndex / 2) + 1;
        uint[] res = new uint[uintLen];
        for (int i = 0; i < uintLen; i++)
        {
            uint low = (uint)(i * 2 < chunks.Length ? chunks[i * 2] : 0);
            uint high = (uint)(i * 2 + 1 < chunks.Length ? chunks[i * 2 + 1] : 0);
            res[i] = low | (high << 16);
        }

        return new BetterBigInteger(res, isNegative);
    }
}
