using System.ComponentModel.DataAnnotations;

namespace Payments.Models;

public class Account
{
    public Guid Id { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    
    public decimal Balance { get; set; }

    /// <summary>
    /// Поле для реализации CAS (Compare and Swap).
    /// EF Core будет проверять это поле при каждом UPDATE, 
    /// чтобы избежать коллизий при параллельном доступе.
    /// </summary>
    [ConcurrencyCheck]
    public Guid Version { get; set; }
}