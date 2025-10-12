using Microsoft.Extensions.Logging;
using ResilientMicroservices.Core;
using System.Threading;
using System.Threading.Tasks;

namespace ResilientMicroservices.Sagas;

public abstract class BaseSaga<TData> : ISaga<TData> where TData : class
{
    private readonly List<ISagaStep<TData>> _steps;
    protected readonly ILogger _logger;

    public string SagaId { get; }

    protected BaseSaga(ILogger logger, string? sagaId = null)
    {
        _logger = logger;
        SagaId = sagaId ?? $"{GetType().Name}-{Guid.NewGuid():N}";
        _steps = new List<ISagaStep<TData>>();
    }

    protected void AddStep(ISagaStep<TData> step)
    {
        _steps.Add(step);
    }

    public virtual async Task<SagaResult> ExecuteAsync(TData data, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing saga {SagaId} with {StepCount} steps", SagaId, _steps.Count);

        var executedSteps = new List<ISagaStep<TData>>();

        try
        {
            // Execute steps in order
            foreach (var step in _steps.OrderBy(s => s.Order))
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogDebug("Executing step {StepName} (order: {Order})", step.StepName, step.Order);

                var stepResult = await step.ExecuteAsync(data, cancellationToken);
                
                if (!stepResult.IsSuccess)
                {
                    _logger.LogWarning("Step {StepName} failed: {ErrorMessage}", step.StepName, stepResult.ErrorMessage);
                    
                    // Compensate executed steps in reverse order
                    await CompensateExecutedStepsAsync(executedSteps, data, cancellationToken);
                    
                    return SagaResult.Failure($"Step {step.StepName} failed: {stepResult.ErrorMessage}", stepResult.Exception);
                }

                executedSteps.Add(step);
                _logger.LogDebug("Step {StepName} completed successfully", step.StepName);
            }

            _logger.LogInformation("All steps completed successfully for saga {SagaId}", SagaId);
            return SagaResult.Success(new Dictionary<string, object> { ["ExecutedSteps"] = executedSteps.Count });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Saga {SagaId} was cancelled", SagaId);
            await CompensateExecutedStepsAsync(executedSteps, data, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in saga {SagaId}", SagaId);
            await CompensateExecutedStepsAsync(executedSteps, data, cancellationToken);
            return SagaResult.Failure($"Unexpected error: {ex.Message}", ex);
        }
    }

    public virtual async Task<SagaResult> CompensateAsync(TData data, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Compensating saga {SagaId}", SagaId);
        
        try
        {
            await CompensateExecutedStepsAsync(_steps, data, cancellationToken);
            return SagaResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during compensation for saga {SagaId}", SagaId);
            return SagaResult.Failure($"Compensation failed: {ex.Message}", ex);
        }
    }

    private async Task CompensateExecutedStepsAsync(List<ISagaStep<TData>> executedSteps, TData data, CancellationToken cancellationToken)
    {
        // Compensate in reverse order
        foreach (var step in executedSteps.OrderByDescending(s => s.Order))
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                _logger.LogDebug("Compensating step {StepName}", step.StepName);
                
                var compensationResult = await step.CompensateAsync(data, cancellationToken);
                
                if (!compensationResult.IsSuccess)
                {
                    _logger.LogError("Compensation failed for step {StepName}: {ErrorMessage}", 
                        step.StepName, compensationResult.ErrorMessage);
                }
                else
                {
                    _logger.LogDebug("Step {StepName} compensated successfully", step.StepName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during compensation of step {StepName}", step.StepName);
                // Continue with other compensations
            }
        }
    }
} 