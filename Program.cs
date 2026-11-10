using System;
using System.Collections.Generic;

// Інтерфейс і клас предметної області

interface IHasAmount
{
    decimal Amount { get; }
}
class Expense : IHasAmount
{
    public string Category { get; }
    public string Shop { get; }
    public DateTime Date { get; }
    public decimal Amount { get; }

    public Expense(string category, string shop, decimal amount, DateTime date)
    {
        Category = category;
        Shop = shop;
        Amount = amount;
        Date = date;
    }

    public override string ToString() =>
        $"{Date:dd.MM.yyyy} | {Category,-12} | {Shop,-12} | {Amount,10:0.00} грн";
}

// Дані події (узагальнений нащадок EventArgs)

class BudgetExceededEventArgs<T> : EventArgs
{
    public T Item { get; }
    public decimal Total { get; }
    public decimal Budget { get; }

    public BudgetExceededEventArgs(T item, decimal total, decimal budget)
    {
        Item = item;
        Total = total;
        Budget = budget;
    }
}
// Узагальнений клас ExpenseLog<T>
class ExpenseLog<T> where T : IHasAmount
{
    private readonly List<T> _items = new();
    private Func<T, string>? _grouping; 

    public decimal Budget { get; }
    public int Count => _items.Count;
    public bool HasGrouping => _grouping != null;

    public event EventHandler<BudgetExceededEventArgs<T>>? BudgetExceeded;

    public ExpenseLog(decimal budget)
    {
        Budget = budget;
    }

    public void Add(T item)
    {
        _items.Add(item);

        decimal total = GetTotal();

        if (total > Budget)
        {
            OnBudgetExceeded(new BudgetExceededEventArgs<T>(item, total, Budget));
        }
    }
    public decimal GetTotal()
    {
        decimal sum = 0;

        foreach (T item in _items)
        {
            sum += item.Amount; // Amount доступний завдяки обмеженню IHasAmount
        }

        return sum;
    }

    public void SetGrouping(Func<T, string>? grouping) => _grouping = grouping;

    public Dictionary<string, decimal> GetReport()
    {
        var report = new Dictionary<string, decimal>();

        foreach (T item in _items)
        {
            // Якщо групування не задано — усе потрапляє в рядок «Усього»
            string key = _grouping?.Invoke(item) ?? "Усього";

            if (report.TryGetValue(key, out decimal current))
            {
                report[key] = current + item.Amount;
            }
            else
            {
                report[key] = item.Amount;
            }
        }

        return report;
    }
    public void ForEach(Action<T> action)
    {
        foreach (T item in _items)
        {
            action(item);
        }
    }

    protected virtual void OnBudgetExceeded(BudgetExceededEventArgs<T> e) =>
        BudgetExceeded?.Invoke(this, e);
}
class Program
{
    // Список покупок понад бюджет 
    static readonly List<Expense> OverBudget = new();

    // Назва поточного групування для виводу
    static string _groupingName = "без групування";

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        // 1. Журнал із бюджетом, заповнений наперед у коді
        var log = new ExpenseLog<Expense>(2000m);
        log.Add(new Expense("Їжа", "АТБ", 450m, new DateTime(2026, 10, 12)));
        log.Add(new Expense("Транспорт", "Укрзалізниця", 600m, new DateTime(2026, 10, 20)));
        log.Add(new Expense("Їжа", "Сільпо", 450m, new DateTime(2026, 11, 3)));

        // 2. Підписники на подію
        log.BudgetExceeded += ShowWarning;                    
        log.BudgetExceeded += (sender, e) => OverBudget.Add(e.Item); 

        // 3. Меню
        bool isRunning = true;

        while (isRunning)
        {
            Console.WriteLine("\n Облік витрат ");
            Console.WriteLine("0. Вихід");
            Console.WriteLine("1. Додати витрату");
            Console.WriteLine("2. Показати всі витрати");
            Console.WriteLine("3. Показати звіт");
            Console.WriteLine("4. Обрати групування");
            Console.WriteLine("5. Показати покупки понад бюджет");

            int choice = ReadInt("Ваш вибір: ", 0, 5);

            isRunning = choice switch
            {
                1 => HandleAdd(log),
                2 => HandleShowAll(log),
                3 => HandleReport(log),
                4 => HandleChooseGrouping(log),
                5 => HandleShowOverBudget(),
                0 => false,
                _ => true
            };
        }

        Console.WriteLine("Роботу завершено.");
    }

    // 4. Обробник події — іменований метод

    static void ShowWarning(object? sender, BudgetExceededEventArgs<Expense> e)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Увага! Витрачено {e.Total:0.##} грн із бюджету {e.Budget:0.##} грн " +
                          $"(перевищення: {e.Total - e.Budget:0.##} грн)");
        Console.ResetColor();
    }

    // 5. Пункти меню

    static bool HandleAdd(ExpenseLog<Expense> log)
    {
        string category = ReadNonEmpty("Категорія: ");
        string shop = ReadNonEmpty("Магазин: ");
        decimal amount = ReadDecimal("Сума (грн): ");

        log.Add(new Expense(category, shop, amount, DateTime.Now)); // тут може спрацювати подія
        Console.WriteLine($"Додано. Загальна сума: {log.GetTotal():0.##} грн із {log.Budget:0.##} грн.");
        return true;
    }

    static bool HandleShowAll(ExpenseLog<Expense> log)
    {
        if (log.Count == 0)
        {
            Console.WriteLine("Немає даних.");
            return true;
        }

        Console.WriteLine("Дата       | Категорія    | Магазин      |       Сума");
        Console.WriteLine(new string('-', 56));

        log.ForEach(e => Console.WriteLine(e));

        Console.WriteLine(new string('-', 56));
        Console.WriteLine($"Разом: {log.GetTotal():0.##} грн (бюджет: {log.Budget:0.##} грн)");
        return true;
    }

    static bool HandleReport(ExpenseLog<Expense> log)
    {
        Console.WriteLine($"Звіт ({_groupingName}):");

        foreach (var pair in log.GetReport())
        {
            Console.WriteLine($"  {pair.Key}: {pair.Value:0.##} грн");
        }

        return true;
    }

    static bool HandleChooseGrouping(ExpenseLog<Expense> log)
    {
        Console.WriteLine("0 - без групування, 1 - за категорією, 2 - за магазином, 3 - за місяцем");
        int mode = ReadInt("Групування: ", 0, 3);

       
        Func<Expense, string>? grouping = mode switch
        {
            1 => e => e.Category,
            2 => e => e.Shop,
            3 => e => e.Date.ToString("yyyy-MM"),
            _ => null
        };

        _groupingName = mode switch
        {
            1 => "за категорією",
            2 => "за магазином",
            3 => "за місяцем",
            _ => "без групування"
        };

        log.SetGrouping(grouping);
        Console.WriteLine($"Встановлено групування: {_groupingName}.");
        return true;
    }

    static bool HandleShowOverBudget()
    {
        if (OverBudget.Count == 0)
        {
            Console.WriteLine("Покупок понад бюджет немає.");
            return true;
        }

        Console.WriteLine("Покупки понад бюджет:");
        OverBudget.ForEach(e => Console.WriteLine($"  {e}")); 
        return true;
    }

    // 6. Допоміжні методи введення

    static string ReadNonEmpty(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            string? input = Console.ReadLine();

            if (!string.IsNullOrWhiteSpace(input))
            {
                return input.Trim();
            }

            Console.WriteLine("Значення не може бути порожнім.");
        }
    }

    static int ReadInt(string prompt, int min = int.MinValue, int max = int.MaxValue)
    {
        while (true)
        {
            Console.Write(prompt);
            string? input = Console.ReadLine();

            if (int.TryParse(input, out int value) && value >= min && value <= max)
            {
                return value;
            }

            Console.WriteLine($"Некоректне значення. Введіть ціле число від {min} до {max}.");
        }
    }

    static decimal ReadDecimal(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            string? input = Console.ReadLine()?.Replace('.', ',');

           
            if (decimal.TryParse(input, System.Globalization.NumberStyles.Number,
                    new System.Globalization.CultureInfo("uk-UA"), out decimal value) && value > 0)
            {
                return value;
            }

            Console.WriteLine("Некоректна сума. Введіть додатне число (наприклад, 250,50).");
        }
    }
}
