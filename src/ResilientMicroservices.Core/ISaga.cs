using System.Threading;
using System.Threading.Tasks;

namespace ResilientMicroservices.Core;

public interface ISaga<TData> where TData : class
{
    Task<SagaResult> ExecuteAsync(TData data, CancellationToken cancellationToken = default);
    Task<SagaResult> CompensateAsync(TData data, CancellationToken cancellationToken = default);
    string SagaId { get; }
}

public interface ISagaStep<TData> where TData : class
{
    Task<StepResult> ExecuteAsync(TData data, CancellationToken cancellationToken = default);
    Task<StepResult> CompensateAsync(TData data, CancellationToken cancellationToken = default);
    string StepName { get; }
    int Order { get; }
}

public interface ISagaOrchestrator
{
    Task<SagaResult> ExecuteSagaAsync<TData>(ISaga<TData> saga, TData data, CancellationToken cancellationToken = default) where TData : class;
}

public class SagaResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public Dictionary<string, object>? Data { get; set; }

    public static SagaResult Success(Dictionary<string, object>? data = null)
        => new() { IsSuccess = true, Data = data };

    public static SagaResult Failure(string errorMessage, Exception? exception = null, Dictionary<string, object>? data = null)
        => new() { IsSuccess = false, ErrorMessage = errorMessage, Exception = exception, Data = data };
}

public class StepResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public bool CanCompensate { get; set; } = true;

    public static StepResult Success() => new() { IsSuccess = true };
    public static StepResult Failure(string errorMessage, Exception? exception = null, bool canCompensate = true)
        => new() { IsSuccess = false, ErrorMessage = errorMessage, Exception = exception, CanCompensate = canCompensate };
} 