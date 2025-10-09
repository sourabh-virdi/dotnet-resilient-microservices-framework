using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ResilientMicroservices.Core;

namespace ResilientMicroservices.Messaging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddResilientMicroservicesMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        // Register core services first
        services.AddResilientMicroservicesCore();

        // Configure RabbitMQ options
        services.Configure<RabbitMqOptions>(options =>
        {
            configuration.GetSection("ResilientMicroservices:RabbitMQ").Bind(options);
            options.ServiceName = serviceName;
        });

        // Register message bus
        services.TryAddSingleton<IMessageBus, RabbitMqMessageBus>();

        return services;
    }

    public static IServiceCollection AddResilientMicroservicesMessaging(
        this IServiceCollection services,
        Action<RabbitMqOptionsBuilder> configure)
    {
        // Register core services first
        services.AddResilientMicroservicesCore();

        var builder = new RabbitMqOptionsBuilder();
        configure(builder);

        // Configure RabbitMQ options
        services.Configure<RabbitMqOptions>(options =>
        {
            options.HostName = builder.HostName;
            options.Port = builder.Port;
            options.UserName = builder.UserName;
            options.Password = builder.Password;
            options.VirtualHost = builder.VirtualHost;
            options.DefaultExchange = builder.DefaultExchange;
            options.ServiceName = builder.ServiceName;
            options.ConnectionName = builder.ConnectionName;
        });

        // Register message bus
        services.TryAddSingleton<IMessageBus, RabbitMqMessageBus>();

        return services;
    }
}

public class RabbitMqOptionsBuilder
{
    public string HostName { get; private set; } = "localhost";
    public int Port { get; private set; } = 5672;
    public string UserName { get; private set; } = "guest";
    public string Password { get; private set; } = "guest";
    public string VirtualHost { get; private set; } = "/";
    public string DefaultExchange { get; private set; } = "microservices.events";
    public string ServiceName { get; private set; } = "unknown";
    public string ConnectionName { get; private set; } = "ResilientMicroservices";

    public RabbitMqOptionsBuilder WithHost(string hostName, int port = 5672)
    {
        HostName = hostName;
        Port = port;
        return this;
    }

    public RabbitMqOptionsBuilder WithCredentials(string userName, string password)
    {
        UserName = userName;
        Password = password;
        return this;
    }

    public RabbitMqOptionsBuilder WithVirtualHost(string virtualHost)
    {
        VirtualHost = virtualHost;
        return this;
    }

    public RabbitMqOptionsBuilder WithExchange(string exchangeName)
    {
        DefaultExchange = exchangeName;
        return this;
    }

    public RabbitMqOptionsBuilder WithServiceName(string serviceName)
    {
        ServiceName = serviceName;
        return this;
    }

    public RabbitMqOptionsBuilder WithConnectionName(string connectionName)
    {
        ConnectionName = connectionName;
        return this;
    }
} 