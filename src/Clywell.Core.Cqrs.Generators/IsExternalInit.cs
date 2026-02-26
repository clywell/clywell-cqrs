// Polyfill required for C# 9+ init-only setters and record types when targeting netstandard2.0.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
