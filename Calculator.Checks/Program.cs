using Calculator;

var evaluator = new ExpressionEvaluator();
Check("2 + 2", 4d);
Check("2 + 3 * 4", 14d);
Check("(2 + 3) * 4", 20d);
Check("5 / 2", 2.5d);
Check("2^3", 8d);
Check("2^3^2", 512d);
Check("-2^2", -4d);
Check("(-2)^2", 4d);
Check("√9", 3d);
Check("√(2 + 7)", 3d);
Check("10%", 0.1d);
Check("200 * 10%", 20d);
Check("10% + 10%", 0.2d);
Check("100 - 5%", 95d);
Check("1,5 + 1.0", 2.5d);
Check("1e308", 1e308d);
Check("1e-323", 1e-323d);
Check("5e-324", double.Epsilon);
Check("cos(90)", 0d);
Check("sin(0.0000000000001)", Math.Sin(0.0000000000001d * (Math.PI / 180d)));
CheckFormatted("0.1 + 0.2", "0.3");
Check(evaluator.FormatResult(evaluator.Evaluate("1/3")) + "*3", 1d);
Check(evaluator.FormatResult(evaluator.Evaluate("10^15")) + "*2", 2e15d);
Check(evaluator.FormatResult(evaluator.Evaluate("1/100000")) + "*100000", 1d);
Check("17976931348623157" + new string('0', 292), double.MaxValue);
CheckIsFinite("170!");
CheckThrows<DivideByZeroException>("5 / 0");
CheckThrows<ArithmeticException>("√-1");
CheckThrows<ArithmeticException>("0^-1");
CheckThrows<ArithmeticException>("1e-324");
CheckThrows<ArithmeticException>("1e-300 * 1e-100");
CheckThrows<ArithmeticException>("1e-300 / 1e100");
CheckThrows<ArithmeticException>("5e-324%");
CheckThrows<ArithmeticException>("1e-323%");
CheckThrows<ArithmeticException>("10^-400");
CheckThrows<ArithmeticException>("10^309");
CheckThrows<ArithmeticException>("171!");
CheckThrows<FormatException>("2 + (");
CheckThrows<FormatException>("2 3");
Console.WriteLine("Все проверки вычислительной логики пройдены.");

void Check(string expression, double expected)
{
    var actual = evaluator.Evaluate(expression);
    if (actual != expected)
        throw new InvalidOperationException($"Для «{expression}» ожидалось {expected}, получено {actual}.");
    Console.WriteLine($"OK: {expression} = {evaluator.FormatResult(actual)}");
}

void CheckThrows<TException>(string expression) where TException : Exception
{
    try
    {
        evaluator.Evaluate(expression);
    }
    catch (TException)
    {
        Console.WriteLine($"OK: «{expression}» вызывает {typeof(TException).Name}");
        return;
    }

    throw new InvalidOperationException($"Для «{expression}» ожидалось исключение {typeof(TException).Name}.");
}

void CheckIsFinite(string expression)
{
    var actual = evaluator.Evaluate(expression);
    if (!double.IsFinite(actual))
        throw new InvalidOperationException($"Для «{expression}» ожидалось конечное число, получено {actual}.");
    Console.WriteLine($"OK: {expression} = {evaluator.FormatResult(actual)} (конечное число)");
}

void CheckFormatted(string expression, string expected)
{
    var actual = evaluator.FormatResult(evaluator.Evaluate(expression));
    if (actual != expected)
        throw new InvalidOperationException($"Для «{expression}» ожидалось отображение {expected}, получено {actual}.");
    Console.WriteLine($"OK: отображение {expression} = {actual}");
}
