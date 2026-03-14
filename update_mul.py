import re

with open("Arithmetic/BigInt/BetterBigInteger.cs", "r") as f:
    code = f.read()

mul_code = """
    public static BetterBigInteger operator *(BetterBigInteger a, BetterBigInteger b)
    {
        int lenA = a.GetDigits().Length;
        int lenB = b.GetDigits().Length;
        int maxLen = Math.Max(lenA, lenB);

        IMultiplier multiplier;
        if (maxLen <= 32)
        {
            multiplier = new SimpleMultiplier();
        }
        else if (maxLen <= 256)
        {
            multiplier = new KaratsubaMultiplier();
        }
        else
        {
            multiplier = new FftMultiplier();
        }

        return multiplier.Multiply(a, b);
    }
"""

code = re.sub(r'public static BetterBigInteger operator \*\(BetterBigInteger a, BetterBigInteger b\)\s*=> throw new NotImplementedException\(.*?\);', mul_code.strip(), code, flags=re.DOTALL)

with open("Arithmetic/BigInt/BetterBigInteger.cs", "w") as f:
    f.write(code)
