namespace FlatPatternExporter.Models;

public interface IDynamicPropertyValueSource
{
    string GetDynamicPropertyValue(string propertyPath);
    bool IsPropertyExpression(string propertyPath);
}
