using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Clywell.Core.Cqrs.Generators;

/// <summary>
/// Roslyn incremental source generator that scans a compilation for concrete
/// <c>ICommandHandler&lt;,&gt;</c> and <c>IQueryHandler&lt;,&gt;</c> implementations
/// and emits compile-time DI registration, replacing reflection-based assembly scanning.
/// </summary>
/// <remarks>
/// <para>
/// The generated <c>AddCqrsHandlers()</c> extension method registers each handler and its
/// corresponding <c>IHandlerInvoker</c> adapter as transient services, so the dispatcher
/// can resolve them without any runtime reflection.
/// </para>
/// <para>
/// Usage in the host project:
/// <code>
/// services.AddCqrs();
/// services.AddCqrsHandlers(); // generated — zero reflection
/// </code>
/// </para>
/// </remarks>
[Generator]
public sealed class CqrsHandlerRegistrationGenerator : IIncrementalGenerator
{
    private const string ICommandHandlerMetadataName = "Clywell.Core.Cqrs.ICommandHandler`2";
    private const string IQueryHandlerMetadataName = "Clywell.Core.Cqrs.IQueryHandler`2";
    private const string ICommandMetadataName = "Clywell.Core.Cqrs.ICommand`1";
    private const string IQueryMetadataName = "Clywell.Core.Cqrs.IQuery`1";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Step 1: resolve the handler interfaces from the compilation
        var baseInterfaces = context.CompilationProvider.Select(
            static (compilation, _) => GetBaseInterfaces(compilation));

        // Step 2: find candidate types — any class declaration with a base list
        var classSyntax = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax c
                    && c.BaseList is not null
                    && c.BaseList.Types.Count > 0,
                transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
            .Where(static c => c is not null);

        // Step 3: combine each class with compilation and base interfaces
        var combined = classSyntax
            .Combine(context.CompilationProvider)
            .Combine(baseInterfaces);

        // Step 4: extract handler registration info
        var registrations = combined
            .Select(static (pair, _) =>
            {
                var ((syntax, compilation), bases) = pair;
                return ExtractRegistrations(syntax, compilation, bases);
            })
            .Where(static r => r.Count > 0);

        // Step 5: collect all and emit a single source file
        var allRegistrations = registrations.Collect();

        context.RegisterSourceOutput(
            allRegistrations.Combine(context.CompilationProvider),
            static (spc, pair) => Emit(spc, pair.Left, pair.Right));
    }

    // ============================================================
    // Symbol helpers
    // ============================================================

    private static (INamedTypeSymbol? ICommandHandler, INamedTypeSymbol? IQueryHandler) GetBaseInterfaces(
        Compilation compilation)
    {
        return (
            compilation.GetTypeByMetadataName(ICommandHandlerMetadataName),
            compilation.GetTypeByMetadataName(IQueryHandlerMetadataName));
    }

    private static IReadOnlyList<HandlerRegistrationInfo> ExtractRegistrations(
        ClassDeclarationSyntax classSyntax,
        Compilation compilation,
        (INamedTypeSymbol? ICommandHandler, INamedTypeSymbol? IQueryHandler) bases)
    {
        if (bases.ICommandHandler is null && bases.IQueryHandler is null)
            return ImmutableArray<HandlerRegistrationInfo>.Empty;

        var model = compilation.GetSemanticModel(classSyntax.SyntaxTree);
        if (model.GetDeclaredSymbol(classSyntax) is not INamedTypeSymbol classSymbol)
            return ImmutableArray<HandlerRegistrationInfo>.Empty;

        // Skip abstract, open generic, and non-class types
        if (classSymbol.IsAbstract || classSymbol.IsGenericType
            || classSymbol.TypeKind != TypeKind.Class)
            return ImmutableArray<HandlerRegistrationInfo>.Empty;

        var results = new List<HandlerRegistrationInfo>();

        foreach (var iface in classSymbol.AllInterfaces)
        {
            if (!iface.IsGenericType || iface.TypeArguments.Length != 2)
                continue;

            var originalDef = iface.OriginalDefinition;
            HandlerKind? kind = null;

            if (SymbolEqualityComparer.Default.Equals(originalDef, bases.ICommandHandler))
                kind = HandlerKind.Command;
            else if (SymbolEqualityComparer.Default.Equals(originalDef, bases.IQueryHandler))
                kind = HandlerKind.Query;

            if (kind is null)
                continue;

            var requestType = iface.TypeArguments[0];
            var resultType = iface.TypeArguments[1];

            results.Add(new HandlerRegistrationInfo(
                Kind: kind.Value,
                HandlerInterfaceFullName: iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                ImplementationFullName: classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                RequestTypeFullName: requestType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                ResultTypeFullName: resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }

        return results;
    }

    // ============================================================
    // Code emission
    // ============================================================

    private static void Emit(
        SourceProductionContext spc,
        ImmutableArray<IReadOnlyList<HandlerRegistrationInfo>> allGroups,
        Compilation compilation)
    {
        // Flatten and deduplicate
        var seen = new HashSet<string>();
        var registrations = new List<HandlerRegistrationInfo>();

        foreach (var group in allGroups)
        {
            foreach (var info in group)
            {
                var key = $"{info.HandlerInterfaceFullName}|{info.ImplementationFullName}";
                if (seen.Add(key))
                    registrations.Add(info);
            }
        }

        if (registrations.Count == 0)
            return;

        var rootNamespace = compilation.AssemblyName ?? "Clywell.Core.Cqrs";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Source-generated CQRS handler DI registrations.");
        sb.AppendLine("    /// Replaces reflection-based assembly scanning with zero-reflection, compile-time registration.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static class CqrsHandlerRegistrationExtensions");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Registers all detected command/query handler implementations and their invoker adapters");
        sb.AppendLine("        /// as transient services. Generated at compile time — no reflection, NativeAOT compatible.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static IServiceCollection AddCqrsHandlers(");
        sb.AppendLine("            this IServiceCollection services)");
        sb.AppendLine("        {");

        foreach (var reg in registrations)
        {
            // Register the handler itself: ICommandHandler<TCmd, TResult> → ConcreteHandler
            sb.AppendLine(
                $"            services.TryAddTransient<{reg.HandlerInterfaceFullName}, {reg.ImplementationFullName}>();");

            // Register the invoker adapter used by the dispatcher
            var invokerInterface =
                $"global::Clywell.Core.Cqrs.Dispatching.IHandlerInvoker<{reg.RequestTypeFullName}, {reg.ResultTypeFullName}>";

            var invokerImpl = reg.Kind == HandlerKind.Command
                ? $"global::Clywell.Core.Cqrs.Dispatching.CommandHandlerInvoker<{reg.RequestTypeFullName}, {reg.ResultTypeFullName}>"
                : $"global::Clywell.Core.Cqrs.Dispatching.QueryHandlerInvoker<{reg.RequestTypeFullName}, {reg.ResultTypeFullName}>";

            sb.AppendLine(
                $"            services.TryAddTransient<{invokerInterface}, {invokerImpl}>();");
            sb.AppendLine();
        }

        sb.AppendLine("            return services;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        spc.AddSource(
            "CqrsHandlerRegistrationExtensions.g.cs",
            SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    // ============================================================
    // Data
    // ============================================================

    private enum HandlerKind
    {
        Command,
        Query,
    }

    private readonly record struct HandlerRegistrationInfo(
        HandlerKind Kind,
        string HandlerInterfaceFullName,
        string ImplementationFullName,
        string RequestTypeFullName,
        string ResultTypeFullName);
}
