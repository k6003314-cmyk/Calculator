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
Check("1,5 + 1.0", 2.5d);
CheckThrows<DivideByZeroException>("5 / 0");
CheckThrows<ArithmeticException>("√-1");
CheckThrows<ArithmeticException>("0^-1");
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
