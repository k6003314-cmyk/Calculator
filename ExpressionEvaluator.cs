using System.Globalization;

namespace Calculator;

/// <summary>Безопасно разбирает арифметическое выражение и вычисляет его.</summary>
public sealed class ExpressionEvaluator
{
    public double Evaluate(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);

        var parser = new Parser(expression.Replace(',', '.'));
        var value = parser.ParseExpression();
        parser.SkipWhiteSpace();

        if (!parser.IsAtEnd)
        {
            throw new FormatException("Проверьте выражение: после числа или скобок остались лишние символы.");
        }

        if (!double.IsFinite(value))
        {
            throw new ArithmeticException("Результат слишком велик или не может быть представлен числом.");
        }

        return value;
    }

    public string FormatResult(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArithmeticException("Результат слишком велик или не может быть представлен числом.");
        }

        return value.ToString("G15", CultureInfo.InvariantCulture);
    }

    private sealed class Parser(string text)
    {
        private int _position;

        public bool IsAtEnd => _position >= text.Length;

        public double ParseExpression()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipWhiteSpace();
                if (Match('+')) value += ParseTerm();
                else if (Match('-')) value -= ParseTerm();
                else return value;
                EnsureFinite(value);
            }
        }

        private double ParseTerm()
        {
            var value = ParseUnary();
            while (true)
            {
                SkipWhiteSpace();
                if (Match('*')) value *= ParseUnary();
                else if (Match('/'))
                {
                    var divisor = ParseUnary();
                    if (divisor == 0d) throw new DivideByZeroException("На ноль делить нельзя.");
                    value /= divisor;
                }
                else return value;
                EnsureFinite(value);
            }
        }

        private double ParseUnary()
        {
            SkipWhiteSpace();
            if (Match('+')) return ParseUnary();
            if (Match('-')) return -ParseUnary();
            if (Match('√'))
            {
                var radicand = ParseUnary();
                if (radicand < 0d) throw new ArithmeticException("Нельзя извлечь квадратный корень из отрицательного числа.");
                return Math.Sqrt(radicand);
            }
            var value = ParsePower();
            while (Match('%')) value /= 100d;
            return value;
        }

        private double ParsePower()
        {
            var value = ParsePrimary();
            SkipWhiteSpace();
            if (!Match('^')) return value;

            var exponent = ParseUnary();
            if (value == 0d && exponent < 0d)
                throw new ArithmeticException("Ноль нельзя возводить в отрицательную степень.");

            value = Math.Pow(value, exponent);
            EnsureFinite(value);
            return value;
        }

        private double ParsePrimary()
        {
            SkipWhiteSpace();
            if (Match('('))
            {
                var value = ParseExpression();
                SkipWhiteSpace();
                if (!Match(')')) throw new FormatException("Не найдена закрывающая скобка.");
                return value;
            }

            return ParseNumber();
        }

        private double ParseNumber()
        {
            SkipWhiteSpace();
            var start = _position;
            var hasDigit = false;

            while (!IsAtEnd && char.IsAsciiDigit(text[_position]))
            {
                hasDigit = true;
                _position++;
            }

            if (!IsAtEnd && text[_position] == '.')
            {
                _position++;
                while (!IsAtEnd && char.IsAsciiDigit(text[_position]))
                {
                    hasDigit = true;
                    _position++;
                }
            }

            if (!hasDigit)
            {
                throw new FormatException("Ожидалось число или открывающая скобка.");
            }

            var token = text[start.._position];
            if (!double.TryParse(token, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException("Не удалось прочитать число.");
            }

            EnsureFinite(value);
            return value;
        }

        public void SkipWhiteSpace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(text[_position])) _position++;
        }

        private bool Match(char expected)
        {
            if (IsAtEnd || text[_position] != expected) return false;
            _position++;
            return true;
        }

        private static void EnsureFinite(double value)
        {
            if (!double.IsFinite(value))
            {
                throw new ArithmeticException("Результат слишком велик или не может быть представлен числом.");
            }
        }
    }
}
