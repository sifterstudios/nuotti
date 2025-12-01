using System.Net;
using System.Net.Sockets;

namespace Nuotti.Backend.TimeDrift;

/// <summary>
/// Service to detect time drift between server time and OS/NTP time.
/// </summary>
public class TimeDriftService
{
    private readonly ILogger<TimeDriftService> _logger;

    public TimeDriftService(ILogger<TimeDriftService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Checks time drift using best-effort NTP check.
    /// Returns the drift in milliseconds.
    /// </summary>
    public TimeDriftResult CheckTimeDrift()
    {
        try
        {
            // Best-effort NTP check using pool.ntp.org
            var ntpServers = new[] { "pool.ntp.org", "time.windows.com", "time.google.com" };
            foreach (var server in ntpServers)
            {
                try
                {
                    var ntpTime = GetNtpTime(server);
                    if (ntpTime.HasValue)
                    {
                        var localTime = DateTimeOffset.UtcNow;
                        var drift = (localTime - ntpTime.Value).TotalMilliseconds;

                        return new TimeDriftResult
                        {
                            DriftMs = drift,
                            NtpServer = server,
                            LocalTime = localTime,
                            NtpTime = ntpTime.Value,
                            Success = true
                        };
                    }
                }
                catch (System.Exception ex)
                {
                    _logger.LogDebug("Failed to get time from NTP server {Server}: {Message}", server, ex.Message);
                    continue;
                }
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning("Time drift check failed: {Message}", ex.Message);
        }

        // Fallback: compare system time to DateTimeOffset.UtcNow (they should be the same)
        var now = DateTimeOffset.UtcNow;
        return new TimeDriftResult
        {
            DriftMs = 0, // Can't detect drift without NTP
            Success = false,
            LocalTime = now,
            Error = "Unable to reach NTP servers"
        };
    }

    private DateTimeOffset? GetNtpTime(string server)
    {
        try
        {
            // Simplified NTP check - best effort only
            // This is a basic implementation; in production you might want a more robust NTP library
            IPEndPoint? endPoint = null;
            try
            {
                var addresses = Dns.GetHostAddresses(server);
                if (addresses.Length == 0) return null;
                endPoint = new IPEndPoint(addresses[0], 123); // NTP port
            }
            catch
            {
                return null;
            }

            using var client = new UdpClient();
            client.Client.ReceiveTimeout = 2000; // 2 second timeout

            var ntpData = new byte[48];
            ntpData[0] = 0x1B; // NTP request packet

            client.Send(ntpData, ntpData.Length, endPoint);

            var response = client.Receive(ref endPoint);
            if (response.Length >= 48)
            {
                // Extract timestamp from NTP response (bytes 40-43: seconds, 44-47: fraction)
                var intPart = BitConverter.ToUInt32(response, 40);
                var fractPart = BitConverter.ToUInt32(response, 44);

                // Convert from network byte order (big-endian) to host byte order
                if (BitConverter.IsLittleEndian)
                {
                    intPart = (uint)IPAddress.NetworkToHostOrder((int)intPart);
                    fractPart = (uint)IPAddress.NetworkToHostOrder((int)fractPart);
                }

                // NTP epoch is January 1, 1900
                var ntpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var seconds = intPart;
                var milliseconds = (fractPart * 1000UL) / 0x100000000UL;

                return ntpEpoch.AddSeconds(seconds).AddMilliseconds((long)milliseconds);
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogDebug("NTP query failed for {Server}: {Message}", server, ex.Message);
        }

        return null;
    }

    /// <summary>
    /// Classifies drift severity.
    /// </summary>
    public static string ClassifyDrift(double driftMs)
    {
        return Math.Abs(driftMs) switch
        {
            < 50 => "Normal",
            < 250 => "Minor",
            < 1000 => "Significant",
            _ => "Critical"
        };
    }
}

public class TimeDriftResult
{
    public double DriftMs { get; set; }
    public string? NtpServer { get; set; }
    public DateTimeOffset LocalTime { get; set; }
    public DateTimeOffset? NtpTime { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

