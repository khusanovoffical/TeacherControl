using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SchoolControl.Server.Services;

public sealed class DiscoveryService : BackgroundService
{
    private readonly ILogger<DiscoveryService> _logger;
    private readonly IConfiguration _config;
    private const int DiscoveryPort = 5124;

    public DiscoveryService(ILogger<DiscoveryService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("UDP Discovery Service ishga tushdi, port: {Port}", DiscoveryPort);
        using var client = new UdpClient();
        client.EnableBroadcast = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var port = _config["PORT"] ?? "5123";
                var beacon = $"SCHOOLCONTROL_BEACON:{port}";
                var bytes = Encoding.UTF8.GetBytes(beacon);
                var endpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
                await client.SendAsync(bytes, bytes.Length, endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("UDP beacon yuborishda xatolik: {Message}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
}
