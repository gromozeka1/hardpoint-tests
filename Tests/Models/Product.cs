namespace Tests.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Price { get; set; }
    public ProductType ProductType { get; set; }
    public int Stock { get; set; }
}

public enum ProductType
{
    CPU,
    MONITOR,
    PERIPHARALS,
    EXTERNAL
}
