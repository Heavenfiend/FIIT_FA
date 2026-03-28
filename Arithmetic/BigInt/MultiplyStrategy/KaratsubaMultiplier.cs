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
        if (a.Length == 0 || b.Length == 0) return Array.Empty<uint>(); // ноль равен

        if (a.Length <= Threshold || b.Length <= Threshold)
        {
            return SimpleMultiply(a, b); // если маленькие числа
        }

        int m = Math.Max(a.Length, b.Length) / 2;

        ReadOnlySpan<uint> low1 = a.Slice(0, Math.Min(m, a.Length)); // режем пополам (может вязть все число)
        ReadOnlySpan<uint> high1 = m < a.Length ? a.Slice(m) : ReadOnlySpan<uint>.Empty; // закидываем high

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

    private uint[] SimpleMultiply(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b) // тот же метод столбиком только на входе у нас спаны для чтения 
    {
        if (a.Length == 0 || b.Length == 0) return Array.Empty<uint>();
        uint[] res = new uint[a.Length + b.Length];

        for (int i = 0; i < a.Length; i++)
        {
            uint carry = 0;
            uint ai = a[i];

            uint aL = ai & 0xFFFF;
            uint aH = ai >> 16;

            for (int j = 0; j < b.Length; j++)
            {
                uint bj = b[j];
                uint bL = bj & 0xFFFF;
                uint bH = bj >> 16;

                uint p0 = aL * bL;
                uint p1 = aL * bH;
                uint p2 = aH * bL;
                uint p3 = aH * bH;

                uint resL = res[i + j] & 0xFFFF;
                uint resH = res[i + j] >> 16;
                uint carryL = carry & 0xFFFF;
                uint carryH = carry >> 16;

                uint lowSum = (p0 & 0xFFFF) + resL + carryL;
                uint lowCarry = lowSum >> 16;

                uint midSum = (p0 >> 16) + (p1 & 0xFFFF) + (p2 & 0xFFFF) + resH + carryH + lowCarry;
                uint midCarry = midSum >> 16;

                uint highSum = p3 + (p1 >> 16) + (p2 >> 16) + midCarry;

                res[i + j] = (midSum << 16) | (lowSum & 0xFFFF);
                carry = highSum;
            }
            if (carry > 0)
            {
                res[i + b.Length] += carry;
            }
        }
        return res;
    }

    private uint[] Add(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b) // обычное сложение 
    {
        int maxLen = Math.Max(a.Length, b.Length);
        uint[] res = new uint[maxLen + 1]; // на размер больше на всякий
        uint carry = 0;
        for (int i = 0; i < maxLen || carry > 0; i++)
        {
            uint aDigit = (i < a.Length) ? a[i] : 0;
            uint bDigit = (i < b.Length) ? b[i] : 0;

            uint aLow = aDigit & 0xFFFF;
            uint aHigh = aDigit >> 16;

            uint bLow = bDigit & 0xFFFF;
            uint bHigh = bDigit >> 16;

            uint sumLow = aLow + bLow + carry;
            uint sumHigh = aHigh + bHigh + (sumLow >> 16);

            res[i] = (sumHigh << 16) | (sumLow & 0xFFFF);
            carry = sumHigh >> 16;
        }
        return res;
    }

    private uint[] Subtract(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b) // обычное вычитание 
    {
        uint[] res = new uint[a.Length];
        uint borrow = 0;
        for (int i = 0; i < a.Length; i++)
        {
            uint aDigit = a[i];
            uint bDigit = (i < b.Length) ? b[i] : 0;

            uint diff = aDigit - bDigit - borrow;

            if (aDigit < bDigit || (aDigit == bDigit && borrow > 0))
            {
                borrow = 1;
            }
            else
            {
                borrow = 0;
            }
            res[i] = diff;
        }
        return res;
    }

    private uint[] Combine(ReadOnlySpan<uint> z0, ReadOnlySpan<uint> z1, ReadOnlySpan<uint> z2, int m)
    {
        int len = Math.Max(z0.Length, Math.Max(z1.Length + m, z2.Length + 2 * m)) + 1; // ищем для длины
        uint[] res = new uint[len];
        // сложение со сдвигами
        AddInPlace(res, z0, 0); 
        AddInPlace(res, z1, m);
        AddInPlace(res, z2, 2 * m);

        return res;
    }

    private void AddInPlace(uint[] res, ReadOnlySpan<uint> val, int offset)
    {
        uint carry = 0;
        for (int i = 0; i < val.Length || carry > 0; i++)
        {
            if (offset + i >= res.Length) break; // проверка на выход за пределы
            
            uint resDigit = res[offset + i];
            uint valDigit = (i < val.Length) ? val[i] : 0;

            uint resLow = resDigit & 0xFFFF;
            uint resHigh = resDigit >> 16;

            uint valLow = valDigit & 0xFFFF;
            uint valHigh = valDigit >> 16;

            uint sumLow = resLow + valLow + carry;
            uint sumHigh = resHigh + valHigh + (sumLow >> 16);

            res[offset + i] = (sumHigh << 16) | (sumLow & 0xFFFF); // записываем в массив
            carry = sumHigh >> 16; // перенос
        }
    }
}