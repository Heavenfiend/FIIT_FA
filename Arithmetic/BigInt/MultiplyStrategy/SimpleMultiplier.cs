using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class SimpleMultiplier : IMultiplier
{
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        var da = a.GetDigits();
        var db = b.GetDigits();

        if (da.Length == 1 && da[0] == 0) return new BetterBigInteger(new uint[] { 0 }); // равен ноль значит рез ноль будет
        if (db.Length == 1 && db[0] == 0) return new BetterBigInteger(new uint[] { 0 });

        uint[] res = new uint[da.Length + db.Length]; // длина складывается из длин

        for (int i = 0; i < da.Length; i++)
        {
            uint carry = 0; 
            uint ai = da[i]; 
            
            // Разбиваем ai на половинки (чтобы не делать это на каждой итерации j)
            uint aL = ai & 0xFFFF;
            uint aH = ai >> 16;

            for (int j = 0; j < db.Length; j++)
            {
                uint dbj = db[j];
                uint bL = dbj & 0xFFFF;
                uint bH = dbj >> 16;

                // Делаем 4 микро-умножения 
                uint p0 = aL * bL;
                uint p1 = aL * bH;
                uint p2 = aH * bL;
                uint p3 = aH * bH;

                // Достаем то, что уже накопилось в массиве, и текущий перенос
                uint resL = res[i + j] & 0xFFFF;
                uint resH = res[i + j] >> 16;
                uint carryL = carry & 0xFFFF;
                uint carryH = carry >> 16;

                // Складываем части, учитывая переносы между ними
                uint lowSum = (p0 & 0xFFFF) + resL + carryL;
                uint lowCarry = lowSum >> 16;

                uint midSum = (p0 >> 16) + (p1 & 0xFFFF) + (p2 & 0xFFFF) + resH + carryH + lowCarry;
                uint midCarry = midSum >> 16;

                uint highSum = p3 + (p1 >> 16) + (p2 >> 16) + midCarry;

                res[i + j] = (midSum << 16) | (lowSum & 0xFFFF);
                carry = highSum; // перенос в след разряд
            }
            if (carry > 0)
            {
                res[i + db.Length] += carry; // остаток записываем ВАЖНО что индекс + ДЛИНА
            }
        }

        return new BetterBigInteger(res, a.IsNegative != b.IsNegative);
    }
}
