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

        if (da.Length == 1 && da[0] == 0) return new BetterBigInteger(new uint[] { 0 }); // ноль 
        if (db.Length == 1 && db[0] == 0) return new BetterBigInteger(new uint[] { 0 });

        ushort[] aChunks = ToUshortChunks(da); // на два по 16 бит чтобы влез в dble
        ushort[] bChunks = ToUshortChunks(db);

        int maxChunks = aChunks.Length + bChunks.Length;
        int n = 1;
        while (n < maxChunks) n <<= 1; //  находим ближ степень 2

        Complex[] ca = new Complex[n];
        for (int i = 0; i < aChunks.Length; i++) ca[i] = new Complex(aChunks[i], 0); // создаем массивы комплексных чисел

        Complex[] cb = new Complex[n];
        for (int i = 0; i < bChunks.Length; i++) cb[i] = new Complex(bChunks[i], 0);

        Fft(ca, false); // перевод в точки
        Fft(cb, false);

        for (int i = 0; i < n; i++)
        {
            ca[i] = ca[i] * cb[i]; // 1*1 2*2 и т.д. точки
        }

        Fft(ca, true); // обратно из фурье
        
        double[] resChunks = new double[n];
        for (int i = 0; i < n; i++)
        {
            resChunks[i] = Math.Round(ca[i].Real); // округляем
        }

        return FromChunks(resChunks, a.IsNegative != b.IsNegative); // итог
    }

    private static ushort[] ToUshortChunks(ReadOnlySpan<uint> digits) 
    {
        ushort[] chunks = new ushort[digits.Length * 2];
        for (int i = 0; i < digits.Length; i++)
        {
            chunks[i * 2] = (ushort)(digits[i] & 0xFFFF); // первые 16 
            chunks[i * 2 + 1] = (ushort)(digits[i] >> 16); // следующие 
        }
        return chunks;
    }

    private static void Fft(Complex[] a, bool invert) // получаем массив с точками (значениями в комплексных корнях)
    {
        int n = a.Length;
        for (int i = 1, j = 0; i < n; i++) // битовая реверсия для деления массива (001 и 100)
        {
            int bit = n >> 1; // прибавим единицу к числу j слева
            for (; (j & bit) != 0; bit >>= 1)
                j ^= bit;
            j ^= bit; 

            if (i < j) // меняем местами только когда i меньше j 
            {
                var temp = a[i];
                a[i] = a[j];
                a[j] = temp;
            }
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = 2 * Math.PI / len * (invert ? -1 : 1); // делим круг на длину группы
            Complex wlen = new Complex(Math.Cos(angle), Math.Sin(angle)); // шаг
            for (int i = 0; i < n; i += len)
            {
                Complex w = new Complex(1, 0);
                for (int j = 0; j < len / 2; j++)
                {
                    Complex u = a[i + j]; // эл из лев половины
                    Complex v = a[i + j + len / 2] * w; // эл из правой пол умнож на поворот
                    a[i + j] = u + v; // В левую ячейку записываем сумму
                    a[i + j + len / 2] = u - v; // в правую - разность
                    w *= wlen; // поворачиваем для след пары
                }
            }
        }

        if (invert) // если мы из фурье в норм число
        {
            for (int i = 0; i < n; i++)
                a[i] /= n;
        }
    }

    private static BetterBigInteger FromChunks(double[] chunks, bool isNegative) // превращаем в нормальное число
    {
        double carry = 0; // перенос теперь тоже double
        int maxIndex = -1;
        
        // Массив для хранения готовых 16-битных блоков
        uint[] uintChunks = new uint[chunks.Length];

        for (int i = 0; i < chunks.Length; i++) // аналог сложения в столбик(перенос все что больше 16 бит)
        {
            double val = chunks[i] + carry;
            
            double nextCarry = Math.Floor(val / 65536.0); // аналог >> 16
            uintChunks[i] = (uint)(val - nextCarry * 65536.0); // остаток 
            
            carry = nextCarry;
            if (uintChunks[i] != 0) maxIndex = i;
        }

        while (carry > 0) // дописываем переполнение в конец
        {
            maxIndex++;
            double nextCarry = Math.Floor(carry / 65536.0);
            uint remainder = (uint)(carry - nextCarry * 65536.0);

            if (maxIndex < uintChunks.Length)
            {
                uintChunks[maxIndex] = remainder;
            }
            else
            {
                Array.Resize(ref uintChunks, uintChunks.Length + 1);
                uintChunks[uintChunks.Length - 1] = remainder;
            }
            carry = nextCarry;
        }

        if (maxIndex < 0) return new BetterBigInteger(new uint[] { 0 }); // число состоит из 0

        int uintLen = (maxIndex / 2) + 1;
        uint[] res = new uint[uintLen];
        for (int i = 0; i < uintLen; i++)
        {
            uint low = (uint)(i * 2 < uintChunks.Length ? uintChunks[i * 2] : 0);
            uint high = (uint)(i * 2 + 1 < uintChunks.Length ? uintChunks[i * 2 + 1] : 0);
            res[i] = low | (high << 16); // склеиваем тлоько high вставляем на 16 бит позже
        }

        return new BetterBigInteger(res, isNegative);
    }
}