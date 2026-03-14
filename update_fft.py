import re

with open("Arithmetic/BigInt/MultiplyStrategy/FftMultiplier.cs", "r") as f:
    code = f.read()

# I see a bug in the Fft implementation. The variable names and array indexing is slightly flawed. Let's fix Fft.
fft_code = """
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
"""

code = re.sub(r'private static void Fft\(Complex\[\] a, bool invert\).*?return new BetterBigInteger\(res, isNegative\);\s*}', fft_code.strip() + '\n', code, flags=re.DOTALL)

with open("Arithmetic/BigInt/MultiplyStrategy/FftMultiplier.cs", "w") as f:
    f.write(code)
