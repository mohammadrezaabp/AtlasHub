namespace Hub.Application.Configuration;

public sealed class BudgetOptions
{
    public decimal DailyUsd { get; set; } = 1m;
    public decimal MaxRequestUsd { get; set; } = 0.05m;
}
