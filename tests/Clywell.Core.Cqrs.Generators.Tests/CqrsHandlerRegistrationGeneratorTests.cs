using System.Reflection;
using Clywell.Core.Cqrs.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Clywell.Core.Cqrs.Generators.Tests;

public class CqrsHandlerRegistrationGeneratorTests
{
    private static GeneratorDriverRunResult RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Provide the minimal set of references the compilation needs
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new CqrsHandlerRegistrationGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out _, out _);

        return driver.GetRunResult();
    }

    /// <summary>
    /// Provides the minimal CQRS interface stubs so the generator can resolve types.
    /// These simulate the real Clywell.Core.Cqrs interfaces without requiring
    /// a binary reference to that assembly.
    /// </summary>
    private const string CqrsStubs = """
        namespace Clywell.Core.Cqrs
        {
            public interface ICommand<TResult> { }
            public interface IQuery<TResult> { }
            public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
            {
                System.Threading.Tasks.Task<TResult> HandleAsync(TCommand command, System.Threading.CancellationToken ct = default);
            }
            public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
            {
                System.Threading.Tasks.Task<TResult> HandleAsync(TQuery query, System.Threading.CancellationToken ct = default);
            }
        }
        namespace Clywell.Core.Cqrs.Dispatching
        {
            public interface IHandlerInvoker<in TRequest, TResult>
            {
                System.Threading.Tasks.Task<TResult> HandleAsync(TRequest request, System.Threading.CancellationToken ct);
            }
            public sealed class CommandHandlerInvoker<TCommand, TResult> : IHandlerInvoker<TCommand, TResult>
                where TCommand : Clywell.Core.Cqrs.ICommand<TResult>
            {
                public CommandHandlerInvoker(Clywell.Core.Cqrs.ICommandHandler<TCommand, TResult> handler) { }
                public System.Threading.Tasks.Task<TResult> HandleAsync(TCommand request, System.Threading.CancellationToken ct) => throw new System.NotImplementedException();
            }
            public sealed class QueryHandlerInvoker<TQuery, TResult> : IHandlerInvoker<TQuery, TResult>
                where TQuery : Clywell.Core.Cqrs.IQuery<TResult>
            {
                public QueryHandlerInvoker(Clywell.Core.Cqrs.IQueryHandler<TQuery, TResult> handler) { }
                public System.Threading.Tasks.Task<TResult> HandleAsync(TQuery request, System.Threading.CancellationToken ct) => throw new System.NotImplementedException();
            }
        }
        namespace Microsoft.Extensions.DependencyInjection
        {
            public interface IServiceCollection { }
            public static class ServiceCollectionServiceExtensions { }
        }
        namespace Microsoft.Extensions.DependencyInjection.Extensions
        {
            public static class ServiceCollectionDescriptorExtensions
            {
                public static void TryAddTransient<TService, TImpl>(this Microsoft.Extensions.DependencyInjection.IServiceCollection services) where TImpl : class, TService where TService : class { }
            }
        }
        """;

    [Fact]
    public void DoesNotEmit_WhenNoHandlersExist()
    {
        var source = CqrsStubs + """
            namespace TestApp
            {
                public class NotAHandler { }
            }
            """;

        var result = RunGenerator(source);

        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void EmitsRegistration_ForCommandHandler()
    {
        var source = CqrsStubs + """
            namespace TestApp
            {
                using Clywell.Core.Cqrs;

                public record CreateItemCommand(string Name) : ICommand<string>;

                public class CreateItemHandler : ICommandHandler<CreateItemCommand, string>
                {
                    public System.Threading.Tasks.Task<string> HandleAsync(CreateItemCommand command, System.Threading.CancellationToken ct = default)
                        => System.Threading.Tasks.Task.FromResult("ok");
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Single(result.GeneratedTrees);
        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("AddCqrsHandlers", generatedCode);
        Assert.Contains("CreateItemHandler", generatedCode);
        Assert.Contains("CommandHandlerInvoker", generatedCode);
    }

    [Fact]
    public void EmitsRegistration_ForQueryHandler()
    {
        var source = CqrsStubs + """
            namespace TestApp
            {
                using Clywell.Core.Cqrs;

                public record GetItemQuery(System.Guid Id) : IQuery<string>;

                public class GetItemHandler : IQueryHandler<GetItemQuery, string>
                {
                    public System.Threading.Tasks.Task<string> HandleAsync(GetItemQuery query, System.Threading.CancellationToken ct = default)
                        => System.Threading.Tasks.Task.FromResult("item");
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Single(result.GeneratedTrees);
        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("AddCqrsHandlers", generatedCode);
        Assert.Contains("GetItemHandler", generatedCode);
        Assert.Contains("QueryHandlerInvoker", generatedCode);
    }

    [Fact]
    public void EmitsRegistrations_ForMultipleHandlers()
    {
        var source = CqrsStubs + """
            namespace TestApp
            {
                using Clywell.Core.Cqrs;

                public record CmdA(string V) : ICommand<string>;
                public record CmdB(int V) : ICommand<int>;
                public record QryA(string V) : IQuery<string>;

                public class HandlerA : ICommandHandler<CmdA, string>
                {
                    public System.Threading.Tasks.Task<string> HandleAsync(CmdA command, System.Threading.CancellationToken ct = default) => throw new System.NotImplementedException();
                }
                public class HandlerB : ICommandHandler<CmdB, int>
                {
                    public System.Threading.Tasks.Task<int> HandleAsync(CmdB command, System.Threading.CancellationToken ct = default) => throw new System.NotImplementedException();
                }
                public class HandlerC : IQueryHandler<QryA, string>
                {
                    public System.Threading.Tasks.Task<string> HandleAsync(QryA query, System.Threading.CancellationToken ct = default) => throw new System.NotImplementedException();
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Single(result.GeneratedTrees);
        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("HandlerA", generatedCode);
        Assert.Contains("HandlerB", generatedCode);
        Assert.Contains("HandlerC", generatedCode);
        Assert.Contains("CommandHandlerInvoker", generatedCode);
        Assert.Contains("QueryHandlerInvoker", generatedCode);
    }

    [Fact]
    public void SkipsAbstractHandlers()
    {
        var source = CqrsStubs + """
            namespace TestApp
            {
                using Clywell.Core.Cqrs;

                public record MyCommand(string V) : ICommand<string>;

                public abstract class BaseHandler : ICommandHandler<MyCommand, string>
                {
                    public abstract System.Threading.Tasks.Task<string> HandleAsync(MyCommand command, System.Threading.CancellationToken ct = default);
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void SkipsOpenGenericHandlers()
    {
        var source = CqrsStubs + """
            namespace TestApp
            {
                using Clywell.Core.Cqrs;

                public class GenericHandler<T> : ICommandHandler<T, string> where T : ICommand<string>
                {
                    public System.Threading.Tasks.Task<string> HandleAsync(T command, System.Threading.CancellationToken ct = default) => throw new System.NotImplementedException();
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void GeneratedCode_UsesAssemblyNameAsNamespace()
    {
        var source = CqrsStubs + """
            namespace TestApp
            {
                using Clywell.Core.Cqrs;

                public record Cmd(string V) : ICommand<string>;

                public class CmdHandler : ICommandHandler<Cmd, string>
                {
                    public System.Threading.Tasks.Task<string> HandleAsync(Cmd command, System.Threading.CancellationToken ct = default) => throw new System.NotImplementedException();
                }
            }
            """;

        var result = RunGenerator(source);
        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        // Assembly name is "TestAssembly" (set in RunGenerator)
        Assert.Contains("namespace TestAssembly", generatedCode);
    }

    [Fact]
    public void GeneratedCode_IsAutoGeneratedMarked()
    {
        var source = CqrsStubs + """
            namespace TestApp
            {
                using Clywell.Core.Cqrs;

                public record Cmd(string V) : ICommand<string>;

                public class CmdHandler : ICommandHandler<Cmd, string>
                {
                    public System.Threading.Tasks.Task<string> HandleAsync(Cmd command, System.Threading.CancellationToken ct = default) => throw new System.NotImplementedException();
                }
            }
            """;

        var result = RunGenerator(source);
        var generatedCode = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("// <auto-generated/>", generatedCode);
    }
}
