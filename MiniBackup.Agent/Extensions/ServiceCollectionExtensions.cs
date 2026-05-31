using Microsoft.Extensions.DependencyInjection;
using MiniBackup.Agent.Services;
using Polly;
using Polly.Extensions.Http;

namespace MiniBackup.Agent.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNetworkServices(this IServiceCollection services)
    {
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                3,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (outcome, timespan, retryAttempt, context) =>
                {
                    Serilog.Log.Warning("Network error. Attempt {RetryAttempt} of 3. Waiting {Seconds} seconds before retry...",
                        retryAttempt, timespan.TotalSeconds);
                });

        services.AddHttpClient<INetworkClient, NetworkClient>()
                .AddPolicyHandler(retryPolicy);

        return services;
    }
}