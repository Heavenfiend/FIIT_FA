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
    
    private uint _smallValue; 
    private uint[]? _data;
    
    public bool IsNegative => _signBit == 1;
    
    public BetterBigInteger(uint[] digits, bool isNegative = false)
    {
        Initialize(new ReadOnlySpan<uint>(digits), isNegative); // только прочитать
    }
    
    public BetterBigInteger(IEnumerable<uint> digits, bool isNegative = false)
    {
        Initialize(new ReadOnlySpan<uint>(digits.ToArray()), isNegative); // затратнее так как мы копируем в массив
    }

    private void Initialize(ReadOnlySpan<uint> digits, bool isNegative) // инициализируем 
    {
        int length = digits.Length;
        while (length > 0 && digits[length - 1] == 0) // нули лишние в массиве little endian
        {
            length--;
        }

        if (length == 0) // ноль
        {
            _signBit = 0;
            _smallValue = 0;
            _data = null;
        }
        else if (length == 1) // одно число
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
                _data = digits.Slice(0, length).ToArray(); // обрезаем от 0 до length
            }
            catch (OutOfMemoryException e)
            {
                throw new InvalidOperationException("Not enough memory to allocate BetterBigInteger data.", e);
            }
        }
    }
    
    public BetterBigInteger(string value, int radix) // превращает строку в длинное число
    {
        if (string.IsNullOrWhiteSpace(value)) // пусто или нет
            throw new ArgumentException("Value cannot be null or empty.", nameof(value));

        if (radix < 2 || radix > 36) // проверка на осн
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

        if (startIndex >= value.Length) // ваще не число
            throw new FormatException("Value is not a valid number.");

        List<uint> result = new List<uint> { 0 };
        for (int i = startIndex; i < value.Length; i++)
        {
            char c = value[i];
            uint digitValue; // просто проходим и считываем
            if (c >= '0' && c <= '9') digitValue = (uint)(c - '0');
            else if (c >= 'a' && c <= 'z') digitValue = (uint)(c - 'a' + 10);
            else if (c >= 'A' && c <= 'Z') digitValue = (uint)(c - 'A' + 10);
            else throw new FormatException($"Invalid character '{c}' in number.");

            if (digitValue >= radix)
                throw new FormatException($"Invalid character '{c}' for radix {radix}.");

            MultiplyByRadixAndAdd(result, (uint)radix, digitValue);  // возьми то, что уже есть, умножь на основание системы
        }

        Initialize(CollectionsMarshal.AsSpan(result), isNegative);
    }
    
    private static void MultiplyByRadixAndAdd(List<uint> number, uint radix, uint add)
    {
        ulong carry = add;
        var span = CollectionsMarshal.AsSpan(number); // прямой доступ к памяти
        for (int i = 0; i < span.Length; i++)
        {
            ulong current = span[i] * (ulong)radix + carry;
            span[i] = (uint)current;
            carry = current >> 32; // то что вышло за пределы
        } 
        while (carry > 0) // тогда будет еще один разряд 
        {
            number.Add((uint)carry);
            carry >>= 32;
        }
    }
    
    public ReadOnlySpan<uint> GetDigits()
    {
        if (_data != null) return new ReadOnlySpan<uint>(_data); // если уже существует массив 
        return MemoryMarshal.CreateReadOnlySpan(ref _smallValue, 1); // делаем вид что массив из 1 эл
    }

    public int CompareTo(IBigInteger? other) // проверка на равенство(заглушка)
    {
        if (other is null) return 1;
        if (other is not BetterBigInteger b) throw new ArgumentException("Must be BetterBigInteger", nameof(other));
        return CompareTo(b);
    }

    public int CompareTo(BetterBigInteger? other)
    {
        if (other is null) return 1;
        
        if (IsNegative && !other.IsNegative) return -1;
        if (!IsNegative && other.IsNegative) return 1;

        int cmp = CompareMagnitude(this, other); // сравнения по модулям
        return IsNegative ? -cmp : cmp;
    }

    private static int CompareMagnitude(BetterBigInteger a, BetterBigInteger b) // обычное сравнение модулей
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

    public bool Equals(IBigInteger? other) // это проверка на объект 
    {
        if (other is BetterBigInteger b) return Equals(b);
        return false;
    }

    public bool Equals(BetterBigInteger? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true; // один и тот же объект в памяти

        if (_signBit != other._signBit) return false;

        return GetDigits().SequenceEqual(other.GetDigits()); // сравнение цифр
    }

    public override bool Equals(object? obj) => obj is BetterBigInteger other && Equals(other); // правильно работал с другими функциями

    public override int GetHashCode() // создание хэш кода
    {
        HashCode hash = new HashCode();
        hash.Add(_signBit);
        foreach (uint digit in GetDigits())
        {
            hash.Add(digit);
        }
        return hash.ToHashCode();
    }
    
    
    
    
    
    public static BetterBigInteger operator *(BetterBigInteger a, BetterBigInteger b) // выбираем стратегию
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
            if (cmp == 0) return new BetterBigInteger(new uint[] { 0 }); // если по модулю одинаковые и знаки разные 
            var res = SubtractMagnitudes(cmp > 0 ? a : b, cmp > 0 ? b : a);
            return new BetterBigInteger(res, cmp > 0 ? a.IsNegative : b.IsNegative); // из большего вычитаем меньшее и знак сохраняем большего
        }
    }

    public static BetterBigInteger operator -(BetterBigInteger a, BetterBigInteger b) // ну просто сложение с -
    {
        return a + (-b);
    }

    public static BetterBigInteger operator -(BetterBigInteger a)
    {
        if (a.GetDigits().Length == 1 && a.GetDigits()[0] == 0) return a;
        return new BetterBigInteger(a.GetDigits().ToArray(), !a.IsNegative); // меняем знак числа
    }

    private static uint[] AddMagnitudes(BetterBigInteger a, BetterBigInteger b)
    {
        var da = a.GetDigits();
        var db = b.GetDigits();
        int maxLen = Math.Max(da.Length, db.Length); // макс длина цифр
        List<uint> res = new List<uint>(maxLen + 1);
        ulong carry = 0;
        for (int i = 0; i < maxLen || carry > 0; i++) // складываем как в школе с переносом(carry)
        {
            ulong sum = carry;
            if (i < da.Length) sum += da[i];
            if (i < db.Length) sum += db[i];
            res.Add((uint)sum); // добавляем
            carry = sum >> 32; // все что выехало за 32 бит
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
            long diff = dl[i] - borrow; // вычитаем, что заняли на прошлом шаге
            if (i < ds.Length) diff -= ds[i]; 
            if (diff < 0) // если < 0, то занимаем
            {
                diff += 0x100000000L; // прибавляем 2^32
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

    public static BetterBigInteger operator /(BetterBigInteger a, BetterBigInteger b) // просто берем от див мода частное
    {
        DivMod(a, b, out var q, out var r);
        return q;
    }

    public static BetterBigInteger operator %(BetterBigInteger a, BetterBigInteger b) // просто берем от див мода остаток
    {
        DivMod(a, b, out var q, out var r);
        return r;
    }

    private static void DivMod(BetterBigInteger num, BetterBigInteger den, out BetterBigInteger q, out BetterBigInteger r)
    {
        var dDen = den.GetDigits();
        if (dDen.Length == 1 && dDen[0] == 0) // если знаменатель - 0
            throw new DivideByZeroException("Attempted to divide by zero.");

        var dNum = num.GetDigits();
        if (dNum.Length == 1 && dNum[0] == 0) // если числитель 0, то результаты - 0
        {
            q = new BetterBigInteger(new uint[] { 0 });
            r = new BetterBigInteger(new uint[] { 0 });
            return;
        }

        int cmp = CompareMagnitude(num, den);
        if (cmp < 0) // числитель меньше знаменателя, значит весь числитель - остаток
        {
            q = new BetterBigInteger(new uint[] { 0 });
            r = num;
            return;
        }
        if (cmp == 0) // числитель равен знаменателю, значит частное - 1, остаток - 0
        {
            q = new BetterBigInteger(new uint[] { 1 }, num.IsNegative != den.IsNegative);
            r = new BetterBigInteger(new uint[] { 0 });
            return;
        }
        // сюда записываем результат
        BetterBigInteger currentQ = new BetterBigInteger(new uint[] { 0 }); 
        BetterBigInteger currentR = new BetterBigInteger(new uint[] { 0 });

        for (int i = dNum.Length - 1; i >= 0; i--)
        {
            for (int bit = 31; bit >= 0; bit--) // идем по каждому биту в числу
            {
                currentR = (currentR << 1) + new BetterBigInteger(new uint[] { (dNum[i] >> bit) & 1 }); // сдвигаем текущий остаток влево и сносим бит из делимого
                if (CompareMagnitude(currentR, den) >= 0) // можно ли вычесть делитель из накопленного остатка
                {   // вычитаем делитель и записываем 1 в частное
                    currentR = new BetterBigInteger(SubtractMagnitudes(currentR, den), false); 
                    currentQ = (currentQ << 1) + new BetterBigInteger(new uint[] { 1 });
                }
                else
                {
                    currentQ = currentQ << 1; // сдвигаем частное записывая туда 0 
                }
            }
        }

        q = new BetterBigInteger(currentQ.GetDigits().ToArray(), num.IsNegative != den.IsNegative); 
        r = new BetterBigInteger(currentR.GetDigits().ToArray(), num.IsNegative); // знак тот же что и у делимого
    }

    private static uint[] ToTwosComplement(BetterBigInteger val, int length) // доп код(инвертируем и приб 1)
    {
        var digits = val.GetDigits();
        uint[] res = new uint[length]; // создаем массив и копируем туда цифры
        digits.CopyTo(res.AsSpan());

        if (val.IsNegative)
        {
            ulong carry = 1;
            for (int i = 0; i < length; i++)
            {
                ulong sum = (~res[i]) + carry;
                res[i] = (uint)sum;
                carry = sum >> 32; // смотрим нужно ли переносить 1 
            }
        }
        return res;
    }

    private static BetterBigInteger FromTwosComplement(uint[] tc)
    {
        if (tc.Length == 0) return new BetterBigInteger(new uint[] { 0 }); // если пустой массив
        bool isNeg = (tc[^1] & 0x80000000) != 0; // проверка на знак через побитовое и 
        if (!isNeg) return new BetterBigInteger(tc); // ничего не делаем если полож

        uint[] res = new uint[tc.Length]; // инверсия + 1 
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
        uint[] tc = ToTwosComplement(a, len); // в массив битов
        for (int i = 0; i < len; i++) tc[i] = ~tc[i]; // инвертируем биты
        return FromTwosComplement(tc); // возвращает
    }

    public static BetterBigInteger operator &(BetterBigInteger a, BetterBigInteger b)
    {
        int len = Math.Max(a.GetDigits().Length, b.GetDigits().Length) + 1;
        uint[] tca = ToTwosComplement(a, len);
        uint[] tcb = ToTwosComplement(b, len);
        uint[] res = new uint[len];
        for (int i = 0; i < len; i++) res[i] = tca[i] & tcb[i];  // идем по битам и применяем стандартное & 
        return FromTwosComplement(res);
    }

    public static BetterBigInteger operator |(BetterBigInteger a, BetterBigInteger b)
    {
        int len = Math.Max(a.GetDigits().Length, b.GetDigits().Length) + 1;
        uint[] tca = ToTwosComplement(a, len);
        uint[] tcb = ToTwosComplement(b, len);
        uint[] res = new uint[len];
        for (int i = 0; i < len; i++) res[i] = tca[i] | tcb[i]; // идем по битам и применяем стандартное |
        return FromTwosComplement(res);
    }

    public static BetterBigInteger operator ^(BetterBigInteger a, BetterBigInteger b)
    {
        int len = Math.Max(a.GetDigits().Length, b.GetDigits().Length) + 1;
        uint[] tca = ToTwosComplement(a, len);
        uint[] tcb = ToTwosComplement(b, len);
        uint[] res = new uint[len];
        for (int i = 0; i < len; i++) res[i] = tca[i] ^ tcb[i]; // идем по битам и применяем стандартное xor
        return FromTwosComplement(res);
    }

    public static BetterBigInteger operator <<(BetterBigInteger a, int shift) // сдвиг битовый влево
    {
        if (shift == 0) return a;
        if (shift < 0) return a >> (-shift); // отрицат - сдвиг вправо

        var d = a.GetDigits();
        int wordShift = shift / 32; // целых блоков в начало
        int bitShift = shift % 32; // внутри каждого блока

        List<uint> res = new List<uint>(d.Length + wordShift + 1);
        for (int i = 0; i < wordShift; i++) res.Add(0); // заполняем начало нашего списка нулями

        uint carry = 0;
        for (int i = 0; i < d.Length; i++)
        {
            if (bitShift == 0)
            {
                res.Add(d[i]);
            }
            else // если есть малый сдвиг, то даем соседу свои биты
            {
                res.Add((d[i] << bitShift) | carry); 
                carry = d[i] >> (32 - bitShift); // carry - то что мы переносим соседу
            }
        }
        if (carry > 0) res.Add(carry); // если у самого большого блока сдвиг

        return new BetterBigInteger(res.ToArray(), a.IsNegative);
    }

    public static BetterBigInteger operator >>(BetterBigInteger a, int shift)
    {
        if (shift == 0) return a;
        if (shift < 0) return a << (-shift);

        var d = a.GetDigits();
        int wordShift = shift / 32;
        int bitShift = shift % 32;

        if (wordShift >= d.Length) // если сдвигаем на больше бит чем есть то ноль
        {
            if (a.IsNegative) return new BetterBigInteger(new uint[] { 1 }, true);
            return new BetterBigInteger(new uint[] { 0 });
        }

        int newLen = d.Length - wordShift; // выбрасываем блоки которые отлетели
        uint[] res = new uint[newLen];

        uint carry = 0;
        for (int i = d.Length - 1; i >= wordShift; i--)
        {
            int resIdx = i - wordShift;
            if (bitShift == 0)
            {
                res[resIdx] = d[i];
            }
            else // если есть малый сдвиг, то даем соседу свои биты
            {
                res[resIdx] = (d[i] >> bitShift) | carry; 
                carry = d[i] << (32 - bitShift); // carry - то что мы переносим соседу
            }
        }

        var b = new BetterBigInteger(res, a.IsNegative); // проверка округления когда (дробная часть)
        if (a.IsNegative && (a != (b << shift))) 
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
        if (radix < 2 || radix > 36) // проверка на СС
            throw new ArgumentException("Radix must be between 2 and 36.", nameof(radix));

        ReadOnlySpan<uint> digits = GetDigits();
        if (digits.Length == 1 && digits[0] == 0) return "0"; // проверка на ноль

        List<uint> current = new List<uint>(digits.ToArray()); // Создаем копию т.к. алгоритм разрушающий
        List<char> chars = new List<char>(); // тут символы лежаь

        while (current.Count > 0)
        {
            uint rem = DivideByRadix(current, (uint)radix);
            chars.Add(GetHexChar(rem));
            while (current.Count > 0 && current[^1] == 0) // удаляем ведущие нули
            {
                current.RemoveAt(current.Count - 1);
            }
        }

        if (IsNegative) chars.Add('-'); // если отр
        chars.Reverse();// переворот
        return new string(CollectionsMarshal.AsSpan(chars)); // создаем итоговую строку из куска памяти
    }

    private static uint DivideByRadix(List<uint> number, uint radix)
    {
        ulong rem = 0;
        var span = CollectionsMarshal.AsSpan(number);
        for (int i = span.Length - 1; i >= 0; i--)
        {
            ulong current = (rem << 32) | span[i]; // остаток от пред блока с текущим
            span[i] = (uint)(current / radix); // делим это на основание
            rem = current % radix; // новый остаток для след
        }
        return (uint)rem; // и есть то что мы выводим в строку
    }

    private static char GetHexChar(uint val) // перевод в аски
    {
        if (val < 10) return (char)('0' + val);
        return (char)('A' + val - 10);
    }
}
