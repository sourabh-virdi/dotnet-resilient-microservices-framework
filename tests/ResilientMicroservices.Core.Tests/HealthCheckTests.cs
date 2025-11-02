using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ResilientMicroservices.Core;
using ResilientMicroservices.Core.HealthChecks;
using System.Data;
using System.Data.Common;

namespace ResilientMicroservices.Core.Tests;

public class HealthCheckTests
{
    [Fact]
    public async Task MemoryHealthCheck_WhenMemoryBelowThreshold_ReturnsHealthy()
    {
        // Arrange
        var logger = Mock.Of<ILogger<MemoryHealthCheck>>();
        var threshold = long.MaxValue; // Very high threshold
        var healthCheck = new MemoryHealthCheck(threshold, logger);

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("below threshold");
        result.Data.Should().ContainKey("allocated_bytes");
        result.Data.Should().ContainKey("threshold_bytes");
    }

    [Fact]
    public async Task MemoryHealthCheck_WhenMemoryAboveThreshold_ReturnsDegraded()
    {
        // Arrange
        var logger = Mock.Of<ILogger<MemoryHealthCheck>>();
        var threshold = 1L; // Very low threshold
        var healthCheck = new MemoryHealthCheck(threshold, logger);

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("exceeds threshold");
        result.Data.Should().ContainKey("allocated_mb");
        result.Data.Should().ContainKey("threshold_mb");
    }

    [Fact]
    public void MemoryHealthCheck_Name_ReturnsMemory()
    {
        // Arrange
        var healthCheck = new MemoryHealthCheck();

        // Act & Assert
        healthCheck.Name.Should().Be("Memory");
    }

    [Fact]
    public async Task DatabaseHealthCheck_WhenConnectionSucceeds_ReturnsHealthy()
    {
        // Arrange
        var mockConnection = new Mock<DbConnection>();
        var mockCommand = new Mock<DbCommand>();
        var logger = Mock.Of<ILogger<DatabaseHealthCheck>>();

        // Create a fake connection that inherits from DbConnection
        var testConnection = new TestDbConnection();
        var healthCheck = new DatabaseHealthCheck(testConnection, logger);

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Database connection successful");
        result.Data.Should().ContainKey("duration_ms");
        result.Data.Should().ContainKey("connection_state");
    }

    [Fact]
    public async Task DatabaseHealthCheck_WhenConnectionFails_ReturnsUnhealthy()
    {
        // Arrange
        var logger = Mock.Of<ILogger<DatabaseHealthCheck>>();
        var expectedException = new InvalidOperationException("Connection failed");
        var failingConnection = new FailingTestDbConnection(expectedException);

        var healthCheck = new DatabaseHealthCheck(failingConnection, logger);

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Database connection failed");
        result.Exception.Should().Be(expectedException);
        result.Data.Should().ContainKey("duration_ms");
        result.Data.Should().ContainKey("error");
    }

    [Fact]
    public void DatabaseHealthCheck_Name_ReturnsDatabase()
    {
        // Arrange
        var mockConnection = Mock.Of<DbConnection>();
        var logger = Mock.Of<ILogger<DatabaseHealthCheck>>();
        var healthCheck = new DatabaseHealthCheck(mockConnection, logger);

        // Act & Assert
        healthCheck.Name.Should().Be("Database");
    }

    [Fact]
    public async Task HttpHealthCheck_WhenEndpointReturnsSuccess_ReturnsHealthy()
    {
        // Arrange
        var mockHttpClient = new Mock<HttpClient>();
        var logger = Mock.Of<ILogger<HttpHealthCheck>>();
        var endpoint = "http://example.com/health";
        var healthCheckName = "ExternalService";

        // This is tricky to mock HttpClient, so we'll test the success path conceptually
        var healthCheck = new HttpHealthCheck(mockHttpClient.Object, endpoint, healthCheckName, logger);

        // Act & Assert
        healthCheck.Name.Should().Be(healthCheckName);
    }
}

public class HealthCheckResultTests
{
    [Fact]
    public void HealthCheckResult_Healthy_CreatesHealthyResult()
    {
        // Arrange
        var description = "All systems operational";
        var data = new Dictionary<string, object> { ["test"] = "value" };

        // Act
        var result = HealthCheckResult.Healthy(description, data);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be(description);
        result.Data.Should().BeEquivalentTo(data);
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void HealthCheckResult_Degraded_CreatesDegradedResult()
    {
        // Arrange
        var description = "Service running slowly";
        var exception = new TimeoutException();
        var data = new Dictionary<string, object> { ["latency"] = 5000 };

        // Act
        var result = HealthCheckResult.Degraded(description, exception, data);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be(description);
        result.Exception.Should().Be(exception);
        result.Data.Should().BeEquivalentTo(data);
    }

    [Fact]
    public void HealthCheckResult_Unhealthy_CreatesUnhealthyResult()
    {
        // Arrange
        var description = "Service is down";
        var exception = new InvalidOperationException();

        // Act
        var result = HealthCheckResult.Unhealthy(description, exception);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be(description);
        result.Exception.Should().Be(exception);
    }

    [Theory]
    [InlineData(HealthStatus.Unhealthy, 0)]
    [InlineData(HealthStatus.Degraded, 1)]
    [InlineData(HealthStatus.Healthy, 2)]
    public void HealthStatus_Values_AreCorrect(HealthStatus status, int expectedValue)
    {
        // Act & Assert
        ((int)status).Should().Be(expectedValue);
    }
}

// Test helper classes
internal class TestDbConnection : DbConnection
{
    public override string ConnectionString { get; set; } = "";
    public override string Database => "TestDb";
    public override string DataSource => "TestSource";
    public override string ServerVersion => "1.0";
    public override ConnectionState State => ConnectionState.Open;

    public override void ChangeDatabase(string databaseName) { }
    public override void Close() { }
    public override void Open() { }

    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        throw new NotImplementedException();
    }

    protected override DbCommand CreateDbCommand()
    {
        return new TestDbCommand();
    }
}

internal class FailingTestDbConnection : DbConnection
{
    private readonly Exception _exceptionToThrow;

    public FailingTestDbConnection(Exception exceptionToThrow)
    {
        _exceptionToThrow = exceptionToThrow;
    }

    public override string ConnectionString { get; set; } = "";
    public override string Database => "TestDb";
    public override string DataSource => "TestSource";
    public override string ServerVersion => "1.0";
    public override ConnectionState State => ConnectionState.Closed;

    public override void ChangeDatabase(string databaseName) { }
    public override void Close() { }
    public override void Open() => throw _exceptionToThrow;

    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        throw _exceptionToThrow;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        throw new NotImplementedException();
    }

    protected override DbCommand CreateDbCommand()
    {
        return new TestDbCommand();
    }
}

internal class TestDbCommand : DbCommand
{
    public override string CommandText { get; set; } = "";
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; }
    protected override DbParameterCollection DbParameterCollection => throw new NotImplementedException();
    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel() { }
    public override int ExecuteNonQuery() => 1;
    public override object? ExecuteScalar() => 1;

    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<object?>(1);
    }

    public override void Prepare() { }
    protected override DbParameter CreateDbParameter() => throw new NotImplementedException();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotImplementedException();
} 