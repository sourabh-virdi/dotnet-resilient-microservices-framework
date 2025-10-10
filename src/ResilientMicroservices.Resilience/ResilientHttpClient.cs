using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResilientMicroservices.Core;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ResilientMicroservices.Resilience;

public interface IResilientHttpClient
{
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
    Task<T> GetAsync<T>(string requestUri, CancellationToken cancellationToken = default);
    Task<T> PostAsync<T>(string requestUri, HttpContent content, CancellationToken cancellationToken = default);
    Task<T> PutAsync<T>(string requestUri, HttpContent content, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> DeleteAsync(string requestUri, CancellationToken cancellationToken = default);
}

public class ResilientHttpClient : IResilientHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ResilientHttpClient> _logger;
    private readonly IRetryPolicyService _retryService;
    private readonly ITimeoutService _timeoutService;

    public ResilientHttpClient(
        HttpClient httpClient,
        IRetryPolicyService retryService,
        ITimeoutService timeoutService,
        ILogger<ResilientHttpClient> logger)
    {
        _httpClient = httpClient;
        _retryService = retryService;
        _timeoutService = timeoutService;
        _logger = logger;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        return await _retryService.ExecuteWithRetryAsync(async token =>
        {
            _logger.LogDebug("Sending HTTP request to {RequestUri}", request.RequestUri);
            return await _timeoutService.ExecuteWithTimeoutAsync(async timeoutToken =>
            {
                return await _httpClient.SendAsync(request, timeoutToken);
            }, cancellationToken: token);
        }, cancellationToken);
    }

    public async Task<T> GetAsync<T>(string requestUri, CancellationToken cancellationToken = default)
    {
        var response = await _retryService.ExecuteWithRetryAsync(async token =>
        {
            _logger.LogDebug("Sending GET request to {RequestUri}", requestUri);
            return await _timeoutService.ExecuteWithTimeoutAsync(async timeoutToken =>
            {
                return await _httpClient.GetAsync(requestUri, timeoutToken);
            }, cancellationToken: token);
        }, cancellationToken);

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (typeof(T) == typeof(string))
        {
            return (T)(object)content;
        }

        return System.Text.Json.JsonSerializer.Deserialize<T>(content,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    public async Task<T> PostAsync<T>(string requestUri, HttpContent content, CancellationToken cancellationToken = default)
    {
        var response = await _retryService.ExecuteWithRetryAsync(async token =>
        {
            _logger.LogDebug("Sending POST request to {RequestUri}", requestUri);
            return await _timeoutService.ExecuteWithTimeoutAsync(async timeoutToken =>
            {
                return await _httpClient.PostAsync(requestUri, content, timeoutToken);
            }, cancellationToken: token);
        }, cancellationToken);

        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (typeof(T) == typeof(string))
        {
            return (T)(object)responseContent;
        }

        return System.Text.Json.JsonSerializer.Deserialize<T>(responseContent,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    public async Task<T> PutAsync<T>(string requestUri, HttpContent content, CancellationToken cancellationToken = default)
    {
        var response = await _retryService.ExecuteWithRetryAsync(async token =>
        {
            _logger.LogDebug("Sending PUT request to {RequestUri}", requestUri);
            return await _timeoutService.ExecuteWithTimeoutAsync(async timeoutToken =>
            {
                return await _httpClient.PutAsync(requestUri, content, timeoutToken);
            }, cancellationToken: token);
        }, cancellationToken);

        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (typeof(T) == typeof(string))
        {
            return (T)(object)responseContent;
        }

        return System.Text.Json.JsonSerializer.Deserialize<T>(responseContent,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    public async Task<HttpResponseMessage> DeleteAsync(string requestUri, CancellationToken cancellationToken = default)
    {
        return await _retryService.ExecuteWithRetryAsync(async token =>
        {
            _logger.LogDebug("Sending DELETE request to {RequestUri}", requestUri);
            return await _timeoutService.ExecuteWithTimeoutAsync(async timeoutToken =>
            {
                return await _httpClient.DeleteAsync(requestUri, timeoutToken);
            }, cancellationToken: token);
        }, cancellationToken);
    }
} 