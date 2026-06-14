namespace DockLauncher.BuildingBlocks.Domain.Guards;

public static class Guard
{
    public static string AgainstNullOrWhiteSpace(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
        }

        return value;
    }

    public static T AgainstNull<T>(T? value, string parameterName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        return value;
    }
}