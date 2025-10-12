using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ResilientMicroservices.Core;

namespace ResilientMicroservices.Sagas;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddResilientMicroservicesSagas(this IServiceCollection services)
    {
        // Register core services first
        services.AddResilientMicroservicesCore();

        // Register saga orchestrator
        services.TryAddSingleton<ISagaOrchestrator, SagaOrchestrator>();

        return services;
    }

    public static IServiceCollection AddSaga<TData, TSaga>(this IServiceCollection services)
        where TData : class
        where TSaga : class, ISaga<TData>
    {
        services.TryAddTransient<ISaga<TData>, TSaga>();
        return services;
    }

    public static IServiceCollection AddSagaStep<TData, TStep>(this IServiceCollection services)
        where TData : class
        where TStep : class, ISagaStep<TData>
    {
        services.TryAddTransient<ISagaStep<TData>, TStep>();
        return services;
    }
} 