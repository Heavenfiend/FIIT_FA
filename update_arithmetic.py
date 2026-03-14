import re

with open("Arithmetic/BigInt/BetterBigInteger.cs", "r") as f:
    code = f.read()

operators_code = """
    public static BetterBigInteger operator +(BetterBigInteger a, BetterBigInteger b)
    {
        if (a.IsNegative == b.IsNegative)
        {
            var res = AddMagnitudes(a, b);
            return new BetterBigInteger(res, a.IsNegative);
        }
        else
        {
            int cmp = CompareMagnitude(a, b);
            if (cmp == 0) return new BetterBigInteger(new uint[] { 0 });
            var res = SubtractMagnitudes(cmp > 0 ? a : b, cmp > 0 ? b : a);
            return new BetterBigInteger(res, cmp > 0 ? a.IsNegative : b.IsNegative);
        }
    }

    public static BetterBigInteger operator -(BetterBigInteger a, BetterBigInteger b)
    {
        return a + (-b);
    }

    public static BetterBigInteger operator -(BetterBigInteger a)
    {
        if (a.GetDigits().Length == 1 && a.GetDigits()[0] == 0) return a;
        return new BetterBigInteger(a.GetDigits().ToArray(), !a.IsNegative);
    }

    private static uint[] AddMagnitudes(BetterBigInteger a, BetterBigInteger b)
    {
        var da = a.GetDigits();
        var db = b.GetDigits();
        int maxLen = Math.Max(da.Length, db.Length);
        List<uint> res = new List<uint>(maxLen + 1);
        ulong carry = 0;
        for (int i = 0; i < maxLen || carry > 0; i++)
        {
            ulong sum = carry;
            if (i < da.Length) sum += da[i];
            if (i < db.Length) sum += db[i];
            res.Add((uint)sum);
            carry = sum >> 32;
        }
        return res.ToArray();
    }

    private static uint[] SubtractMagnitudes(BetterBigInteger larger, BetterBigInteger smaller)
    {
        var dl = larger.GetDigits();
        var ds = smaller.GetDigits();
        List<uint> res = new List<uint>(dl.Length);
        long borrow = 0;
        for (int i = 0; i < dl.Length; i++)
        {
            long diff = dl[i] - borrow;
            if (i < ds.Length) diff -= ds[i];
            if (diff < 0)
            {
                diff += 0x100000000L;
                borrow = 1;
            }
            else
            {
                borrow = 0;
            }
            res.Add((uint)diff);
        }
        return res.ToArray();
    }

    public static BetterBigInteger operator /(BetterBigInteger a, BetterBigInteger b)
    {
        DivMod(a, b, out var q, out var r);
        return q;
    }

    public static BetterBigInteger operator %(BetterBigInteger a, BetterBigInteger b)
    {
        DivMod(a, b, out var q, out var r);
        return r;
    }

    private static void DivMod(BetterBigInteger num, BetterBigInteger den, out BetterBigInteger q, out BetterBigInteger r)
    {
        var dDen = den.GetDigits();
        if (dDen.Length == 1 && dDen[0] == 0)
            throw new DivideByZeroException("Attempted to divide by zero.");

        var dNum = num.GetDigits();
        if (dNum.Length == 1 && dNum[0] == 0)
        {
            q = new BetterBigInteger(new uint[] { 0 });
            r = new BetterBigInteger(new uint[] { 0 });
            return;
        }

        int cmp = CompareMagnitude(num, den);
        if (cmp < 0)
        {
            q = new BetterBigInteger(new uint[] { 0 });
            r = num;
            return;
        }
        if (cmp == 0)
        {
            q = new BetterBigInteger(new uint[] { 1 }, num.IsNegative != den.IsNegative);
            r = new BetterBigInteger(new uint[] { 0 });
            return;
        }

        BetterBigInteger currentQ = new BetterBigInteger(new uint[] { 0 });
        BetterBigInteger currentR = new BetterBigInteger(new uint[] { 0 });

        for (int i = dNum.Length - 1; i >= 0; i--)
        {
            for (int bit = 31; bit >= 0; bit--)
            {
                currentR = (currentR << 1) + new BetterBigInteger(new uint[] { (dNum[i] >> bit) & 1 });
                if (CompareMagnitude(currentR, den) >= 0)
                {
                    currentR = new BetterBigInteger(SubtractMagnitudes(currentR, den), false); // R is always positive magnitude
                    currentQ = (currentQ << 1) + new BetterBigInteger(new uint[] { 1 });
                }
                else
                {
                    currentQ = currentQ << 1;
                }
            }
        }

        q = new BetterBigInteger(currentQ.GetDigits().ToArray(), num.IsNegative != den.IsNegative);
        r = new BetterBigInteger(currentR.GetDigits().ToArray(), num.IsNegative);
    }

    private static uint[] ToTwosComplement(BetterBigInteger val, int length)
    {
        var digits = val.GetDigits();
        uint[] res = new uint[length];
        digits.CopyTo(res.AsSpan());

        if (val.IsNegative)
        {
            ulong carry = 1;
            for (int i = 0; i < length; i++)
            {
                ulong sum = (~res[i]) + carry;
                res[i] = (uint)sum;
                carry = sum >> 32;
            }
        }
        return res;
    }

    private static BetterBigInteger FromTwosComplement(uint[] tc)
    {
        if (tc.Length == 0) return new BetterBigInteger(new uint[] { 0 });
        bool isNeg = (tc[^1] & 0x80000000) != 0;
        if (!isNeg) return new BetterBigInteger(tc);

        uint[] res = new uint[tc.Length];
        ulong carry = 1;
        for (int i = 0; i < tc.Length; i++)
        {
            ulong sum = (~tc[i]) + carry;
            res[i] = (uint)sum;
            carry = sum >> 32;
        }
        return new BetterBigInteger(res, true);
    }

    public static BetterBigInteger operator ~(BetterBigInteger a)
    {
        int len = a.GetDigits().Length + 1;
        uint[] tc = ToTwosComplement(a, len);
        for (int i = 0; i < len; i++) tc[i] = ~tc[i];
        return FromTwosComplement(tc);
    }

    public static BetterBigInteger operator &(BetterBigInteger a, BetterBigInteger b)
    {
        int len = Math.Max(a.GetDigits().Length, b.GetDigits().Length) + 1;
        uint[] tca = ToTwosComplement(a, len);
        uint[] tcb = ToTwosComplement(b, len);
        uint[] res = new uint[len];
        for (int i = 0; i < len; i++) res[i] = tca[i] & tcb[i];
        return FromTwosComplement(res);
    }

    public static BetterBigInteger operator |(BetterBigInteger a, BetterBigInteger b)
    {
        int len = Math.Max(a.GetDigits().Length, b.GetDigits().Length) + 1;
        uint[] tca = ToTwosComplement(a, len);
        uint[] tcb = ToTwosComplement(b, len);
        uint[] res = new uint[len];
        for (int i = 0; i < len; i++) res[i] = tca[i] | tcb[i];
        return FromTwosComplement(res);
    }

    public static BetterBigInteger operator ^(BetterBigInteger a, BetterBigInteger b)
    {
        int len = Math.Max(a.GetDigits().Length, b.GetDigits().Length) + 1;
        uint[] tca = ToTwosComplement(a, len);
        uint[] tcb = ToTwosComplement(b, len);
        uint[] res = new uint[len];
        for (int i = 0; i < len; i++) res[i] = tca[i] ^ tcb[i];
        return FromTwosComplement(res);
    }

    public static BetterBigInteger operator <<(BetterBigInteger a, int shift)
    {
        if (shift == 0) return a;
        if (shift < 0) return a >> (-shift);

        var d = a.GetDigits();
        int wordShift = shift / 32;
        int bitShift = shift % 32;

        List<uint> res = new List<uint>(d.Length + wordShift + 1);
        for (int i = 0; i < wordShift; i++) res.Add(0);

        uint carry = 0;
        for (int i = 0; i < d.Length; i++)
        {
            if (bitShift == 0)
            {
                res.Add(d[i]);
            }
            else
            {
                res.Add((d[i] << bitShift) | carry);
                carry = d[i] >> (32 - bitShift);
            }
        }
        if (carry > 0) res.Add(carry);

        return new BetterBigInteger(res.ToArray(), a.IsNegative);
    }

    public static BetterBigInteger operator >>(BetterBigInteger a, int shift)
    {
        if (shift == 0) return a;
        if (shift < 0) return a << (-shift);

        var d = a.GetDigits();
        int wordShift = shift / 32;
        int bitShift = shift % 32;

        if (wordShift >= d.Length)
        {
            if (a.IsNegative) return new BetterBigInteger(new uint[] { 1 }, true);
            return new BetterBigInteger(new uint[] { 0 });
        }

        int newLen = d.Length - wordShift;
        uint[] res = new uint[newLen];

        uint carry = 0;
        for (int i = d.Length - 1; i >= wordShift; i--)
        {
            int resIdx = i - wordShift;
            if (bitShift == 0)
            {
                res[resIdx] = d[i];
            }
            else
            {
                res[resIdx] = (d[i] >> bitShift) | carry;
                carry = d[i] << (32 - bitShift);
            }
        }

        var b = new BetterBigInteger(res, a.IsNegative);
        if (a.IsNegative && (a != (b << shift))) // Round towards negative infinity
        {
             b = b - new BetterBigInteger(new uint[] { 1 });
        }
        return b;
    }
"""

code = re.sub(r'public static BetterBigInteger operator \+\(BetterBigInteger a, BetterBigInteger b\) => throw new NotImplementedException\(\);\s*public static BetterBigInteger operator -\(BetterBigInteger a, BetterBigInteger b\) => throw new NotImplementedException\(\);\s*public static BetterBigInteger operator -\(BetterBigInteger a\) => throw new NotImplementedException\(\);\s*public static BetterBigInteger operator /\(BetterBigInteger a, BetterBigInteger b\) => throw new NotImplementedException\(\);\s*public static BetterBigInteger operator %\(BetterBigInteger a, BetterBigInteger b\) => throw new NotImplementedException\(\);', '', code)
code = re.sub(r'public static BetterBigInteger operator ~\(BetterBigInteger a\) => throw new NotImplementedException\(\);\s*public static BetterBigInteger operator &\(BetterBigInteger a, BetterBigInteger b\) => throw new NotImplementedException\(\);\s*public static BetterBigInteger operator \|\(BetterBigInteger a, BetterBigInteger b\) => throw new NotImplementedException\(\);\s*public static BetterBigInteger operator \^\(BetterBigInteger a, BetterBigInteger b\) => throw new NotImplementedException\(\);\s*public static BetterBigInteger operator <<\(BetterBigInteger a, int shift\) => throw new NotImplementedException\(\);\s*public static BetterBigInteger operator >> \(BetterBigInteger a, int shift\) => throw new NotImplementedException\(\);', operators_code, code)

with open("Arithmetic/BigInt/BetterBigInteger.cs", "w") as f:
    f.write(code)
