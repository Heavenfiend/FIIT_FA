using System;
using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class KaratsubaMultiplier : IMultiplier
{
    private const int Threshold = 32;

    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        var res = Multiply(a.GetDigits(), b.GetDigits());
        return new BetterBigInteger(res, a.IsNegative != b.IsNegative);
    }

    private uint[] Multiply(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        if (a.Length == 0 || b.Length == 0) return Array.Empty<uint>();

        if (a.Length <= Threshold || b.Length <= Threshold)
        {
            return SimpleMultiply(a, b);
        }

        int m = Math.Max(a.Length, b.Length) / 2;

        ReadOnlySpan<uint> low1 = a.Slice(0, Math.Min(m, a.Length));
        ReadOnlySpan<uint> high1 = m < a.Length ? a.Slice(m) : ReadOnlySpan<uint>.Empty;

        ReadOnlySpan<uint> low2 = b.Slice(0, Math.Min(m, b.Length));
        ReadOnlySpan<uint> high2 = m < b.Length ? b.Slice(m) : ReadOnlySpan<uint>.Empty;

        uint[] z0 = Multiply(low1, low2);
        uint[] z2 = Multiply(high1, high2);

        uint[] sumA = Add(low1, high1);
        uint[] sumB = Add(low2, high2);
        uint[] z1 = Multiply(sumA, sumB);

        uint[] subZ1 = Subtract(Subtract(z1, z0), z2);

        return Combine(z0, subZ1, z2, m);
    }

    private uint[] SimpleMultiply(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        if (a.Length == 0 || b.Length == 0) return Array.Empty<uint>();
        uint[] res = new uint[a.Length + b.Length];

        for (int i = 0; i < a.Length; i++)
        {
            ulong carry = 0;
            ulong ai = a[i];
            for (int j = 0; j < b.Length; j++)
            {
                ulong prod = ai * b[j] + res[i + j] + carry;
                res[i + j] = (uint)prod;
                carry = prod >> 32;
            }
            if (carry > 0)
            {
                res[i + b.Length] += (uint)carry;
            }
        }
        return res;
    }

    private uint[] Add(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        int maxLen = Math.Max(a.Length, b.Length);
        uint[] res = new uint[maxLen + 1];
        ulong carry = 0;
        for (int i = 0; i < maxLen || carry > 0; i++)
        {
            ulong sum = carry;
            if (i < a.Length) sum += a[i];
            if (i < b.Length) sum += b[i];
            res[i] = (uint)sum;
            carry = sum >> 32;
        }
        return res;
    }

    private uint[] Subtract(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        uint[] res = new uint[a.Length];
        long borrow = 0;
        for (int i = 0; i < a.Length; i++)
        {
            long diff = a[i] - borrow;
            if (i < b.Length) diff -= b[i];
            if (diff < 0)
            {
                diff += 0x100000000L;
                borrow = 1;
            }
            else
            {
                borrow = 0;
            }
            res[i] = (uint)diff;
        }
        return res;
    }

    private uint[] Combine(ReadOnlySpan<uint> z0, ReadOnlySpan<uint> z1, ReadOnlySpan<uint> z2, int m)
    {
        int len = Math.Max(z0.Length, Math.Max(z1.Length + m, z2.Length + 2 * m)) + 1;
        uint[] res = new uint[len];

        AddInPlace(res, z0, 0);
        AddInPlace(res, z1, m);
        AddInPlace(res, z2, 2 * m);

        return res;
    }

    private void AddInPlace(uint[] res, ReadOnlySpan<uint> val, int offset)
    {
        ulong carry = 0;
        for (int i = 0; i < val.Length || carry > 0; i++)
        {
            if (offset + i >= res.Length) break;
            ulong sum = res[offset + i] + carry;
            if (i < val.Length) sum += val[i];
            res[offset + i] = (uint)sum;
            carry = sum >> 32;
        }
    }
}
