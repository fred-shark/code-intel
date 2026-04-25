using System;
using System.Collections.Generic;
using System.IO;
using CodeIntel.Contracts;

namespace CodeIntel.Analysis.Tests;

/// <summary>
/// Проверяет поиск явных DI-регистраций через Roslyn.
/// </summary>
public sealed class FindRegistrationsServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "codeintel-analysis-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FindAsync_ReturnsCastleWindsorGenericRegistration_ForServiceSymbol()
    {
        var solutionPath = CreateRegistrationSolution();
        var service = new FindRegistrationsService();

        var response = await service.FindAsync(solutionPath, "IService");

        Assert.Equal(solutionPath, response.SolutionPath);
        Assert.Equal("IService", response.Symbol);
        Assert.NotNull(response.Declaration);
        Assert.Equal(2, response.RegistrationCount);
        Assert.All(response.Registrations, registration => Assert.Equal(RegistrationFrameworkDto.CastleWindsor, registration.RegistrationFramework));
        Assert.Contains(response.Registrations, registration => registration.Lifetime == "Transient");
        Assert.Contains(response.Registrations, registration => registration.Lifetime == "PerWebRequest");
    }

    [Fact]
    public async Task FindAsync_ReturnsCastleWindsorTypeOfRegistration_ForImplementationSymbol()
    {
        var solutionPath = CreateRegistrationSolution();
        var service = new FindRegistrationsService();

        var response = await service.FindAsync(solutionPath, "Service");

        Assert.NotNull(response.Declaration);
        Assert.Equal(2, response.RegistrationCount);
        Assert.All(response.Registrations, registration => Assert.Contains("global::Sample.Abstractions.Service", registration.ImplementationSymbol, StringComparison.Ordinal));
        Assert.Contains(response.Registrations, registration => registration.Lifetime == "PerWebRequest");
    }

    [Fact]
    public async Task FindAsync_ReturnsOpenGenericWindsorRegistration()
    {
        var solutionPath = CreateRegistrationSolution();
        var service = new FindRegistrationsService();

        var response = await service.FindAsync(solutionPath, "IRepositoryBase");

        var registration = Assert.Single(response.Registrations);
        Assert.Equal("PerWcfOperation", registration.Lifetime);
        Assert.Equal(RegistrationFrameworkDto.CastleWindsor, registration.RegistrationFramework);
        Assert.Contains("IRepositoryBase<T>", registration.ServiceSymbol, StringComparison.Ordinal);
        Assert.Contains("RepositoryBase<T>", registration.ImplementationSymbol, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindAsync_ReturnsEmptyPayload_WhenSymbolIsNotFound()
    {
        var solutionPath = CreateRegistrationSolution();
        var service = new FindRegistrationsService();

        var response = await service.FindAsync(solutionPath, "DefinitelyMissingType");

        Assert.Equal(solutionPath, response.SolutionPath);
        Assert.Null(response.Declaration);
        Assert.Empty(response.Registrations);
        Assert.Equal(0, response.RegistrationCount);
    }

    [Fact]
    public async Task FindAsync_IgnoresUnsupportedCastleConventionRegistrations()
    {
        var solutionPath = CreateRegistrationSolution();
        var service = new FindRegistrationsService();

        var response = await service.FindAsync(solutionPath, "IService");

        Assert.Equal(2, response.Registrations.Count);
        Assert.DoesNotContain(response.Registrations, registration => registration.ServiceSymbol.Contains("DefaultInterfaces", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FindAsync_PreservesAspNetCoreRegistrations()
    {
        var solutionPath = CreateRegistrationSolution();
        var service = new FindRegistrationsService();

        var response = await service.FindAsync(solutionPath, "IAppService");

        var registration = Assert.Single(response.Registrations);
        Assert.Equal(RegistrationFrameworkDto.AspNetCoreDI, registration.RegistrationFramework);
        Assert.Equal("Transient", registration.Lifetime);
        Assert.Contains("IAppService", registration.ServiceSymbol, StringComparison.Ordinal);
        Assert.Contains("AppService", registration.ImplementationSymbol, StringComparison.Ordinal);
        Assert.True(registration.Line > 0);
        Assert.True(registration.Column > 0);
    }

    /// <summary>
    /// Проверяет, что fully qualified запрос снимает неоднозначность при поиске регистраций.
    /// </summary>
    [Fact]
    public async Task FindAsync_ReturnsRegistrations_ForFullyQualifiedSymbol()
    {
        var solutionPath = CreateQualifiedRegistrationSolution();
        var service = new FindRegistrationsService();

        var response = await service.FindAsync(solutionPath, "Sample.Second.IService");

        Assert.NotNull(response.Declaration);
        Assert.Equal("global::Sample.Second.IService", response.Declaration.FullyQualifiedName);
        var registration = Assert.Single(response.Registrations);
        Assert.Contains("global::Sample.Second.IService", registration.ServiceSymbol, StringComparison.Ordinal);
        Assert.Contains("global::Sample.Second.Service", registration.ImplementationSymbol, StringComparison.Ordinal);
    }

    /// <summary>
    /// Проверяет поддержку fully qualified запроса с префиксом global::.
    /// </summary>
    [Fact]
    public async Task FindAsync_ReturnsRegistrations_ForGlobalQualifiedSymbol()
    {
        var solutionPath = CreateQualifiedRegistrationSolution();
        var service = new FindRegistrationsService();

        var response = await service.FindAsync(solutionPath, "global::Sample.Second.IService");

        Assert.NotNull(response.Declaration);
        Assert.Equal("global::Sample.Second.IService", response.Declaration.FullyQualifiedName);
        Assert.Single(response.Registrations);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private string CreateRegistrationSolution()
    {
        var files = new Dictionary<string, string>
        {
            ["Abstractions.cs"] = """
                namespace Sample.Abstractions;

                public interface IService
                {
                }

                public sealed class Service : IService
                {
                }

                public interface IAppService
                {
                }

                public sealed class AppService : IAppService
                {
                }

                public interface IRepositoryBase<T>
                {
                }

                public sealed class RepositoryBase<T> : IRepositoryBase<T>
                {
                }

                public interface IRepositorySynonymBase<T>
                {
                }

                public sealed class RepositorySynonymBase<T> : IRepositorySynonymBase<T>
                {
                }
                """,
            ["MicrosoftDiStubs.cs"] = """
                using System;

                namespace Microsoft.Extensions.DependencyInjection;

                public interface IServiceCollection
                {
                }

                public sealed class ServiceCollection : IServiceCollection
                {
                }

                public static class ServiceCollectionServiceExtensions
                {
                    public static IServiceCollection AddTransient<TService, TImplementation>(this IServiceCollection services)
                        where TImplementation : TService
                    {
                        return services;
                    }

                    public static IServiceCollection AddScoped(this IServiceCollection services, Type serviceType, Type implementationType)
                    {
                        return services;
                    }

                    public static IServiceCollection AddSingleton(this IServiceCollection services, Type serviceType, Type implementationType)
                    {
                        return services;
                    }
                }
                """,
            ["CastleStubs.cs"] = """
                using System;

                namespace Castle.MicroKernel.Registration;

                public interface IRegistration
                {
                }

                public interface IWindsorContainer
                {
                    IWindsorContainer Register(params IRegistration[] registrations);
                }

                public sealed class WindsorContainer : IWindsorContainer
                {
                    public IWindsorContainer Register(params IRegistration[] registrations)
                    {
                        return this;
                    }
                }

                public sealed class ComponentRegistration<TService> : IRegistration
                {
                    public ComponentRegistration<TService> ImplementedBy<TImplementation>()
                    {
                        return this;
                    }

                    public ComponentRegistration<TService> ImplementedBy(Type implementationType)
                    {
                        return this;
                    }

                    public ComponentRegistration<TService> Named(string name)
                    {
                        return this;
                    }

                    public ComponentRegistration<TService> LifestyleTransient()
                    {
                        return this;
                    }

                    public ComponentRegistration<TService> LifestylePerWebRequest()
                    {
                        return this;
                    }

                    public ComponentRegistration<TService> LifestylePerWcfOperation()
                    {
                        return this;
                    }
                }

                public static class Component
                {
                    public static ComponentRegistration<TService> For<TService>()
                    {
                        return new ComponentRegistration<TService>();
                    }

                    public static ComponentRegistration<object> For(Type serviceType)
                    {
                        return new ComponentRegistration<object>();
                    }
                }

                public sealed class FromAssemblyDescriptor : IRegistration
                {
                    public FromAssemblyDescriptor BasedOn<TService>()
                    {
                        return this;
                    }

                    public WithServiceDescriptor WithService => new();
                }

                public sealed class WithServiceDescriptor : IRegistration
                {
                    public WithServiceDescriptor DefaultInterfaces()
                    {
                        return this;
                    }

                    public WithServiceDescriptor FirstInterface()
                    {
                        return this;
                    }
                }

                public static class Classes
                {
                    public static FromAssemblyDescriptor FromAssemblyContaining<T>()
                    {
                        return new FromAssemblyDescriptor();
                    }
                }
                """,
            ["Registrations.cs"] = """
                using Castle.MicroKernel.Registration;
                using Microsoft.Extensions.DependencyInjection;
                using Sample.Abstractions;

                namespace Sample.App;

                public sealed class CompositionRoot
                {
                    public void Configure(IServiceCollection services, IWindsorContainer container)
                    {
                        services.AddTransient<IAppService, AppService>();
                        container.Register(Component.For<IService>().ImplementedBy<Service>().LifestyleTransient());
                        container.Register(Component.For(typeof(IService)).ImplementedBy(typeof(Service)).LifestylePerWebRequest());
                        container.Register(Component.For(typeof(IRepositoryBase<>)).ImplementedBy(typeof(RepositoryBase<>)).Named("repositoryBase").LifestylePerWcfOperation());
                        container.Register(Component.For(typeof(IRepositorySynonymBase<>)).ImplementedBy(typeof(RepositorySynonymBase<>)).Named("repositorySynonymBase").LifestylePerWcfOperation());
                        container.Register(Classes.FromAssemblyContaining<Service>().BasedOn<IService>().WithService.DefaultInterfaces());
                    }
                }
                """
        };

        return CreateSolution(files);
    }

    private string CreateQualifiedRegistrationSolution()
    {
        var files = new Dictionary<string, string>
        {
            ["Services.cs"] = """
                namespace Sample.First
                {
                    public interface IService
                    {
                    }

                    public sealed class Service : IService
                    {
                    }
                }

                namespace Sample.Second
                {
                    public interface IService
                    {
                    }

                    public sealed class Service : IService
                    {
                    }
                }
                """,
            ["CastleStubs.cs"] = """
                namespace Castle.MicroKernel.Registration;

                public interface IRegistration
                {
                }

                public interface IWindsorContainer
                {
                    IWindsorContainer Register(params IRegistration[] registrations);
                }

                public sealed class WindsorContainer : IWindsorContainer
                {
                    public IWindsorContainer Register(params IRegistration[] registrations)
                    {
                        return this;
                    }
                }

                public sealed class ComponentRegistration<TService> : IRegistration
                {
                    public ComponentRegistration<TService> ImplementedBy<TImplementation>()
                    {
                        return this;
                    }
                }

                public static class Component
                {
                    public static ComponentRegistration<TService> For<TService>()
                    {
                        return new ComponentRegistration<TService>();
                    }
                }
                """,
            ["CompositionRoot.cs"] = """
                using Castle.MicroKernel.Registration;

                namespace Sample.App;

                public static class CompositionRoot
                {
                    public static void Configure(IWindsorContainer container)
                    {
                        container.Register(Component.For<Sample.First.IService>().ImplementedBy<Sample.First.Service>());
                        container.Register(Component.For<Sample.Second.IService>().ImplementedBy<Sample.Second.Service>());
                    }
                }
                """
        };

        return CreateSolution(files);
    }

    private string CreateSolution(IReadOnlyDictionary<string, string> sources)
    {
        Directory.CreateDirectory(_tempRoot);
        var projectDir = Path.Combine(_tempRoot, "src", "App");
        Directory.CreateDirectory(projectDir);

        foreach (var (fileName, content) in sources)
        {
            File.WriteAllText(Path.Combine(projectDir, fileName), content);
        }

        var projectPath = Path.Combine(projectDir, "App.csproj");
        File.WriteAllText(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var solutionPath = Path.Combine(_tempRoot, "sample.slnx");
        File.WriteAllText(
            solutionPath,
            """
            <Solution>
              <Project Path="src/App/App.csproj" />
            </Solution>
            """);

        return solutionPath;
    }
}
