using System.Numerics;
using System.Xml.Schema;

namespace Pool;

public static partial class Helper
{
    public static IEnumerable<T> StepRange<T>(T start, T end, Func<T, T> step)
        where T : IComparable
    {
        while (start.CompareTo(end) <= 0)
        {
            yield return start;
            start = step(start);
        }
    }

    public static Type GetNumericType(this ValueType value) => value switch
        {
            TypeCode.Byte => typeof(int),
            TypeCode.SByte => typeof(int),
            TypeCode.UInt16 => typeof(int),
            TypeCode.UInt32 => typeof(int),
            TypeCode.UInt64 => typeof(long),
            TypeCode.Int16 => typeof(int),
            TypeCode.Int32 => typeof(int),
            TypeCode.Int64 => typeof(long),
            TypeCode.Decimal => typeof(double),
            TypeCode.Double => typeof(double),
            TypeCode.Single => typeof(double),
            BigInteger => typeof(BigInteger),
            _ => throw new NotImplementedException()
        };

    public static double ToInterval(this int i, int total, (double First, double Last) diapason)
    {
        if (i < 0 || i > total)
            throw new ArgumentOutOfRangeException(nameof(i), i,
                $"{i} - вне диапазона [0; {total}].");
        if (diapason.First >= diapason.Last)
            throw new ArgumentOutOfRangeException(nameof(diapason), diapason,
                $"Начальная точка диапазона должна быть меньше конечной.");
        return i * (diapason.Last - diapason.First) / total + diapason.First;
    }

    public static double ToInterval(this int i, int total)
    {
        if (i < 0 || i > total)
            throw new ArgumentOutOfRangeException(nameof(i), i,
                $"{i} - вне диапазона [0; {total}].");
        return (double)i / total;
    }

    public static double EquationOfStraightLine(this int i, double a, double b) => a * i + b;
}