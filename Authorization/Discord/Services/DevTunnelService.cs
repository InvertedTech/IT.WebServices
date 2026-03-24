using System.Text.Json.Nodes;
using CliWrap;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace IT.WebServices.Authorization.Discord.Services;

public class DevTunnelService : BackgroundService
{
    private readonly IServer server;
    private readonly IHostApplicationLifetime hostApplicationLifetime;
    private readonly IConfiguration config;
    private readonly ILogger<DevTunnelService> logger;

    public DevTunnelService(
        IServer server,
        IHostApplicationLifetime hostApplicationLifetime,
        IConfiguration config,
        ILogger<DevTunnelService> logger
    ) : base()
    {
        this.server = server;
        this.hostApplicationLifetime = hostApplicationLifetime;
        this.config = config;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WaitForApplicationStarted();

        var urls = server.Features.Get<IServerAddressesFeature>()!.Addresses;
        var localUrl = urls.FirstOrDefault(u => u.StartsWith("https://")) ?? urls.First(u => u.StartsWith("http://"));
        var fixedUrl = config["NgrokUrl"];

        // If ngrok is already running, reuse it
        var existingUrl = await TryGetNgrokPublicUrl();
        if (existingUrl != null)
        {
            logger.LogInformation("Reconnected to existing ngrok tunnel: {NgrokPublicUrl}", existingUrl);
            await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { });
            return;
        }

        logger.LogInformation("Starting ngrok tunnel for {LocalUrl}", localUrl);
        var ngrokTask = StartNgrokTunnel(localUrl, fixedUrl, stoppingToken);

        var publicUrl = fixedUrl ?? await GetNgrokPublicUrl();
        logger.LogInformation("Public ngrok URL: {NgrokPublicUrl}", publicUrl);

        await ngrokTask;

        logger.LogInformation("Ngrok tunnel stopped");
    }

    private Task WaitForApplicationStarted()
    {
        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hostApplicationLifetime.ApplicationStarted.Register(() => completionSource.TrySetResult());
        return completionSource.Task;
    }

    private CommandTask<CommandResult> StartNgrokTunnel(string localUrl, string? fixedUrl, CancellationToken stoppingToken)
    {
        return Cli.Wrap("ngrok")
            .WithArguments(args =>
            {
                args.Add("http").Add(localUrl).Add("--log").Add("stdout");
                if (!string.IsNullOrEmpty(fixedUrl))
                    args.Add("--url").Add(fixedUrl);
            })
            .WithStandardOutputPipe(PipeTarget.ToDelegate(s => logger.LogDebug(s)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(s => logger.LogError(s)))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(stoppingToken);
    }

    private async Task<string?> TryGetNgrokPublicUrl()
    {
        using var httpClient = new HttpClient();
        try
        {
            var json = await httpClient.GetFromJsonAsync<JsonNode>("http://127.0.0.1:4040/api/tunnels");
            return json["tunnels"].AsArray()
                .Select(e => e["public_url"].GetValue<string>())
                .FirstOrDefault(u => u.StartsWith("https://"));
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> GetNgrokPublicUrl()
    {
        for (var i = 0; i < 10; i++)
        {
            logger.LogDebug("Get ngrok tunnels attempt: {RetryCount}", i + 1);
            var url = await TryGetNgrokPublicUrl();
            if (url != null) return url;
            await Task.Delay(200);
        }

        throw new Exception("Ngrok dashboard did not start in 10 tries");
    }
}