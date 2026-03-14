using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class SimpleMultiplier : IMultiplier
{
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        var da = a.GetDigits();
        var db = b.GetDigits();

        if (da.Length == 1 && da[0] == 0) return new BetterBigInteger(new uint[] { 0 });
        if (db.Length == 1 && db[0] == 0) return new BetterBigInteger(new uint[] { 0 });

        uint[] res = new uint[da.Length + db.Length];

        for (int i = 0; i < da.Length; i++)
        {
            ulong carry = 0;
            ulong ai = da[i];
            for (int j = 0; j < db.Length; j++)
            {
                ulong prod = ai * db[j] + res[i + j] + carry;
                res[i + j] = (uint)prod;
                carry = prod >> 32;
            }
            if (carry > 0)
            {
                res[i + db.Length] += (uint)carry;
            }
        }

        return new BetterBigInteger(res, a.IsNegative != b.IsNegative);
    }
}
