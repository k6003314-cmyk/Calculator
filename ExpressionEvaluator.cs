using System.Globalization;

namespace Calculator;

/// <summary>Безопасно разбирает арифметическое выражение и вычисляет его.</summary>
public sealed class ExpressionEvaluator
{
    public double Evaluate(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);

        var parser = new Parser(expression.Replace(',', '.'));
        var value = parser.ParseExpression().Value;
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

        return value == 0d ? "0" : value.ToString("R", CultureInfo.InvariantCulture);
    }

    private sealed class Parser(string text)
    {
        private int _position;

        public readonly record struct ParsedValue(double Value, bool IsPercentage);

        public bool IsAtEnd => _position >= text.Length;

        public ParsedValue ParseExpression()
        {
            var parsed = ParseTerm();
            var value = parsed.Value;
            var isPercentage = parsed.IsPercentage;
            while (true)
            {
                SkipWhiteSpace();
                if (Match('+'))
                {
                    var right = ParseTerm();
                    value += right.IsPercentage && !isPercentage ? value * right.Value : right.Value;
                    isPercentage = false;
                }
                else if (Match('-'))
                {
                    var right = ParseTerm();
                    value -= right.IsPercentage && !isPercentage ? value * right.Value : right.Value;
                    isPercentage = false;
                }
                else return new ParsedValue(value, isPercentage);
                EnsureFinite(value);
            }
        }

        private ParsedValue ParseTerm()
        {
            var value = ParseUnary();
            var hasMultiplicationOrDivision = false;
            while (true)
            {
                SkipWhiteSpace();
                if (Match('*'))
                {
                    var left = value.Value;
                    var right = ParseUnary().Value;
                    var product = left * right;
                    EnsureNotUnderflow(product, left != 0d && right != 0d);
                    value = new ParsedValue(product, false);
                    hasMultiplicationOrDivision = true;
                }
                else if (Match('/'))
                {
                    var divisor = ParseUnary().Value;
                    if (divisor == 0d) throw new DivideByZeroException("На ноль делить нельзя.");
                    var quotient = value.Value / divisor;
                    EnsureNotUnderflow(quotient, value.Value != 0d);
                    value = new ParsedValue(quotient, false);
                    hasMultiplicationOrDivision = true;
                }
                else return hasMultiplicationOrDivision ? new ParsedValue(value.Value, false) : value;
                EnsureFinite(value.Value);
            }
        }

        private ParsedValue ParseUnary()
        {
            SkipWhiteSpace();
            if (Match('+')) return ParseUnary();
            if (Match('-'))
            {
                var negated = ParseUnary();
                return negated with { Value = -negated.Value };
            }
            if (Match('√'))
            {
                var radicand = ParseUnary().Value;
                if (radicand < 0d) throw new ArithmeticException("Нельзя извлечь квадратный корень из отрицательного числа.");
                return new ParsedValue(Math.Sqrt(radicand), false);
            }
            var value = ParsePower();
            while (true)
            {
                if (Match('%')) value = new ParsedValue(value.Value / 100d, true);
                else if (Match('!')) value = new ParsedValue(CalculateFactorial(value.Value), false);
                else return value;
            }
        }

        private static double CalculateFactorial(double value)
        {
            if (value < 0d || value != Math.Truncate(value))
                throw new ArithmeticException("Факториал определён только для целых неотрицательных чисел.");
            if (value > 170d)
                throw new ArithmeticException("Результат факториала слишком велик.");

            var result = 1d;
            for (var factor = 2; factor <= (int)value; factor++) result *= factor;
            return result;
        }

        private ParsedValue ParsePower()
        {
            var value = ParsePrimary();
            SkipWhiteSpace();
            if (!Match('^')) return value;

            var exponent = ParseUnary().Value;
            if (value.Value == 0d && exponent < 0d)
                throw new ArithmeticException("Ноль нельзя возводить в отрицательную степень.");

            var result = Math.Pow(value.Value, exponent);
            EnsureNotUnderflow(result, value.Value != 0d);
            EnsureFinite(result);
            return new ParsedValue(result, false);
        }

        private ParsedValue ParsePrimary()
        {
            SkipWhiteSpace();
            if (Match('('))
            {
                var value = ParseExpression();
                SkipWhiteSpace();
                if (!Match(')')) throw new FormatException("Не найдена закрывающая скобка.");
                return value;
            }

            if (!IsAtEnd && char.IsAsciiLetter(text[_position]))
                return ParseTrigonometricFunction();

            return ParseNumber();
        }

        private ParsedValue ParseTrigonometricFunction()
        {
            var start = _position;
            while (!IsAtEnd && char.IsAsciiLetter(text[_position])) _position++;
            var function = text[start.._position].ToLowerInvariant();
            if (function is not ("sin" or "cos"))
                throw new FormatException("Поддерживаются функции sin и cos.");

            SkipWhiteSpace();
            if (!Match('(')) throw new FormatException("После sin или cos нужна открывающая скобка.");
            var angle = ParseExpression().Value;
            SkipWhiteSpace();
            if (!Match(')')) throw new FormatException("Не найдена закрывающая скобка функции.");

            var radians = angle * (Math.PI / 180d);
            var result = function == "sin" ? Math.Sin(radians) : Math.Cos(radians);
            var normalizedAngle = angle % 360d;
            if (normalizedAngle > 180d) normalizedAngle -= 360d;
            else if (normalizedAngle < -180d) normalizedAngle += 360d;
            if ((function == "sin" && normalizedAngle % 180d == 0d) ||
                (function == "cos" && Math.Abs(normalizedAngle) == 90d))
                result = 0d;
            EnsureFinite(result);
            return new ParsedValue(result, false);
        }

        private ParsedValue ParseNumber()
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

            if (!IsAtEnd && text[_position] is 'e' or 'E')
            {
                _position++;
                if (!IsAtEnd && text[_position] is '+' or '-') _position++;
                var exponentStart = _position;
                while (!IsAtEnd && char.IsAsciiDigit(text[_position])) _position++;
                if (_position == exponentStart)
                    throw new FormatException("После E в научной записи укажите показатель степени.");
            }

            if (!hasDigit)
            {
                throw new FormatException("Ожидалось число или открывающая скобка.");
            }

            var token = text[start.._position];
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException("Не удалось прочитать число.");
            }

            var exponentIndex = token.IndexOfAny(['e', 'E']);
            var significand = exponentIndex < 0 ? token : token[..exponentIndex];
            if (value == 0d && significand.Any(static character => character is >= '1' and <= '9'))
                throw new ArithmeticException("Число слишком мало и округляется до нуля.");

            EnsureFinite(value);
            return new ParsedValue(value, false);
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

        private static void EnsureNotUnderflow(double value, bool nonZeroExpected)
        {
            if (nonZeroExpected && value == 0d)
                throw new ArithmeticException("Результат слишком мал и округляется до нуля.");
        }
    }
}
