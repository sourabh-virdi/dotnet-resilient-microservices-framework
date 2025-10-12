using Microsoft.Extensions.Logging;
using ResilientMicroservices.Core;
using System.Threading;
using System.Threading.Tasks;

namespace ResilientMicroservices.Sagas;

public class SagaOrchestrator : ISagaOrchestrator
{
    private readonly ILogger<SagaOrchestrator> _logger;

    public SagaOrchestrator(ILogger<SagaOrchestrator> logger)
    {
        _logger = logger;
    }

    public async Task<SagaResult> ExecuteSagaAsync<TData>(ISaga<TData> saga, TData data, CancellationToken cancellationToken = default) where TData : class
    {
        _logger.LogInformation("Starting execution of saga {SagaId}", saga.SagaId);

        try
        {
            var result = await saga.ExecuteAsync(data, cancellationToken);
            
            if (result.IsSuccess)
            {
                _logger.LogInformation("Saga {SagaId} completed successfully", saga.SagaId);
                return result;
            }
            else
            {
                _logger.LogWarning("Saga {SagaId} failed: {ErrorMessage}", saga.SagaId, result.ErrorMessage);
                
                // Attempt compensation
                _logger.LogInformation("Starting compensation for saga {SagaId}", saga.SagaId);
                var compensationResult = await saga.CompensateAsync(data, cancellationToken);
                
                if (compensationResult.IsSuccess)
                {
                    _logger.LogInformation("Compensation for saga {SagaId} completed successfully", saga.SagaId);
                }
                else
                {
                    _logger.LogError("Compensation for saga {SagaId} failed: {ErrorMessage}", saga.SagaId, compensationResult.ErrorMessage);
                }

                return result; // Return the original failure result
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing saga {SagaId}", saga.SagaId);
            
            // Attempt compensation for unexpected errors
            try
            {
                _logger.LogInformation("Starting compensation for saga {SagaId} due to unexpected error", saga.SagaId);
                await saga.CompensateAsync(data, cancellationToken);
            }
            catch (Exception compensationEx)
            {
                _logger.LogError(compensationEx, "Compensation failed for saga {SagaId}", saga.SagaId);
            }

            return SagaResult.Failure($"Unexpected error: {ex.Message}", ex);
        }
    }
} 