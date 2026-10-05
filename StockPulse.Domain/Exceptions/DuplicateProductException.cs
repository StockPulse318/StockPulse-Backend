namespace StockPulse.Domain.Exceptions;

public sealed class DuplicateProductException : Exception
{
    public string FieldName { get; }
    public string Value { get; }

    public DuplicateProductException(string fieldName, string value)
        : base($"Product with {fieldName} '{value}' already exists.")
    {
        FieldName = fieldName;
        Value = value;
    }

    public static DuplicateProductException ForCode(string code) => new("product_code", code);
    public static DuplicateProductException ForName(string name) => new("name", name);
}
