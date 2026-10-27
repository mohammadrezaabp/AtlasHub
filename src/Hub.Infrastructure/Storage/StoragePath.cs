namespace Hub.Infrastructure.Storage;

public static class StoragePath
{
    public static string ResolveKey(string root, string key)
    {
        if (key.Split('/').Any(part => part is "" or "." or "..") || key.Contains('\\') || Path.IsPathRooted(key))
            throw new InvalidOperationException("Invalid storage key.");
        return WithinRoot(root, Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
    }

    public static string WithinRoot(string root, string location)
    {
        var path = Path.GetFullPath(location);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, comparison))
            throw new InvalidOperationException("Stored file is outside the upload directory.");
        return path;
    }
}
