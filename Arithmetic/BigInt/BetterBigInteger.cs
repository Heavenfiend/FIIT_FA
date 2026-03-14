using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Arithmetic.BigInt.Interfaces;
using Arithmetic.BigInt.MultiplyStrategy;

namespace Arithmetic.BigInt;

public sealed class BetterBigInteger : IBigInteger, IComparable<BetterBigInteger>, IEquatable<BetterBigInteger>
{
    private int _signBit;
    
    private uint _smallValue; // Если число маленькое, храним его прямо в этом поле, а _data == null.
    private uint[]? _data;
    
    public bool IsNegative => _signBit == 1;
    
    /// От массива цифр (little endian)
    public BetterBigInteger(uint[] digits, bool isNegative = false)
    {
        Initialize(new ReadOnlySpan<uint>(digits), isNegative);
    }
    
    public BetterBigInteger(IEnumerable<uint> digits, bool isNegative = false)
    {
        Initialize(new ReadOnlySpan<uint>(digits.ToArray()), isNegative);
    }

    private void Initialize(ReadOnlySpan<uint> digits, bool isNegative)
    {
        int length = digits.Length;
        while (length > 0 && digits[length - 1] == 0)
        {
            length--;
        }

        if (length == 0)
        {
            _signBit = 0;
            _smallValue = 0;
            _data = null;
        }
        else if (length == 1)
        {
            _signBit = isNegative ? 1 : 0;
            _smallValue = digits[0];
            _data = null;
        }
        else
        {
            _signBit = isNegative ? 1 : 0;
            _smallValue = 0;
            try
            {
                _data = digits.Slice(0, length).ToArray();
            }
            catch (OutOfMemoryException e)
            {
                throw new InvalidOperationException("Not enough memory to allocate BetterBigInteger data.", e);
            }
        }
    }
    
    public BetterBigInteger(string value, int radix)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null or empty.", nameof(value));

        if (radix < 2 || radix > 36)
            throw new ArgumentException("Radix must be between 2 and 36.", nameof(radix));

        bool isNegative = false;
        int startIndex = 0;

        if (value[0] == '-')
        {
            isNegative = true;
            startIndex = 1;
        }
        else if (value[0] == '+')
        {
            startIndex = 1;
        }

        if (startIndex >= value.Length)
            throw new FormatException("Value is not a valid number.");

        List<uint> result = new List<uint> { 0 };
        for (int i = startIndex; i < value.Length; i++)
        {
            char c = value[i];
            uint digitValue;
            if (c >= '0' && c <= '9') digitValue = (uint)(c - '0');
            else if (c >= 'a' && c <= 'z') digitValue = (uint)(c - 'a' + 10);
            else if (c >= 'A' && c <= 'Z') digitValue = (uint)(c - 'A' + 10);
            else throw new FormatException($"Invalid character '{c}' in number.");

            if (digitValue >= radix)
                throw new FormatException($"Invalid character '{c}' for radix {radix}.");

            MultiplyByRadixAndAdd(result, (uint)radix, digitValue);
        }

        Initialize(CollectionsMarshal.AsSpan(result), isNegative);
    }
    
    private static void MultiplyByRadixAndAdd(List<uint> number, uint radix, uint add)
    {
        ulong carry = add;
        var span = CollectionsMarshal.AsSpan(number);
        for (int i = 0; i < span.Length; i++)
        {
            ulong current = span[i] * (ulong)radix + carry;
            span[i] = (uint)current;
            carry = current >> 32;
        }
        while (carry > 0)
        {
            number.Add((uint)carry);
            carry >>= 32;
        }
    }
    
    public ReadOnlySpan<uint> GetDigits()
    {
        if (_data != null) return new ReadOnlySpan<uint>(_data);
        return MemoryMarshal.CreateReadOnlySpan(ref _smallValue, 1);
    }

    public int CompareTo(IBigInteger? other)
    {
        if (other is null) return 1;
        if (other is not BetterBigInteger b) throw new ArgumentException("Must be BetterBigInteger", nameof(other));
        return CompareTo(b);
    }

    public int CompareTo(BetterBigInteger? other)
    {
        if (other is null) return 1;

        // 0 compare 0 is handled: IsNegative is false for both.
        if (IsNegative && !other.IsNegative) return -1;
        if (!IsNegative && other.IsNegative) return 1;

        int cmp = CompareMagnitude(this, other);
        return IsNegative ? -cmp : cmp;
    }

    private static int CompareMagnitude(BetterBigInteger a, BetterBigInteger b)
    {
        ReadOnlySpan<uint> digitsA = a.GetDigits();
        ReadOnlySpan<uint> digitsB = b.GetDigits();

        if (digitsA.Length > digitsB.Length) return 1;
        if (digitsA.Length < digitsB.Length) return -1;

        for (int i = digitsA.Length - 1; i >= 0; i--)
        {
            if (digitsA[i] > digitsB[i]) return 1;
            if (digitsA[i] < digitsB[i]) return -1;
        }

        return 0;
    }

    public bool Equals(IBigInteger? other)
    {
        if (other is BetterBigInteger b) return Equals(b);
        return false;
    }

    public bool Equals(BetterBigInteger? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (_signBit != other._signBit) return false;

        return GetDigits().SequenceEqual(other.GetDigits());
    }

    public override bool Equals(object? obj) => obj is BetterBigInteger other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hash = new HashCode();
        hash.Add(_signBit);
        foreach (uint digit in GetDigits())
        {
            hash.Add(digit);
        }
        return hash.ToHashCode();
    }
    
    
    
    
    
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


    public static bool operator ==(BetterBigInteger a, BetterBigInteger b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(BetterBigInteger a, BetterBigInteger b) => !(a == b);
    public static bool operator <(BetterBigInteger a, BetterBigInteger b) => a is null ? b is not null : a.CompareTo(b) < 0;
    public static bool operator >(BetterBigInteger a, BetterBigInteger b) => a is not null && a.CompareTo(b) > 0;
    public static bool operator <=(BetterBigInteger a, BetterBigInteger b) => a is null || a.CompareTo(b) <= 0;
    public static bool operator >=(BetterBigInteger a, BetterBigInteger b) => a is null ? b is null : a.CompareTo(b) >= 0;
    
    public override string ToString() => ToString(10);
    public string ToString(int radix)
    {
        if (radix < 2 || radix > 36)
            throw new ArgumentException("Radix must be between 2 and 36.", nameof(radix));

        ReadOnlySpan<uint> digits = GetDigits();
        if (digits.Length == 1 && digits[0] == 0) return "0";

        List<uint> current = new List<uint>(digits.ToArray());
        List<char> chars = new List<char>();

        while (current.Count > 0)
        {
            uint rem = DivideByRadix(current, (uint)radix);
            chars.Add(GetHexChar(rem));
            while (current.Count > 0 && current[^1] == 0)
            {
                current.RemoveAt(current.Count - 1);
            }
        }

        if (IsNegative) chars.Add('-');
        chars.Reverse();
        return new string(CollectionsMarshal.AsSpan(chars));
    }

    private static uint DivideByRadix(List<uint> number, uint radix)
    {
        ulong rem = 0;
        var span = CollectionsMarshal.AsSpan(number);
        for (int i = span.Length - 1; i >= 0; i--)
        {
            ulong current = (rem << 32) | span[i];
            span[i] = (uint)(current / radix);
            rem = current % radix;
        }
        return (uint)rem;
    }

    private static char GetHexChar(uint val)
    {
        if (val < 10) return (char)('0' + val);
        return (char)('A' + val - 10);
    }
}
