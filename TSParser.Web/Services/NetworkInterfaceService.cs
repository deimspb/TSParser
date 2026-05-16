using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TSParser.Web.Services;

/// <summary>IPv4 bind addresses for UDP multicast (maps to <see cref="ParserConfig.MulticastIncomingIp"/>).</summary>
public sealed class NetworkInterfaceService
{
    public sealed record BindOption(string? Address, string Label);

    public IReadOnlyList<BindOption> GetBindOptions()
    {
        var options = new List<BindOption> { new(null, "Any") };
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;

            var name = nic.Name;
            foreach (var uni in nic.GetIPProperties().UnicastAddresses)
            {
                if (uni.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                if (IPAddress.IsLoopback(uni.Address))
                    continue;

                var text = uni.Address.ToString();
                if (!seen.Add(text))
                    continue;

                options.Add(new BindOption(text, $"{text} ({name})"));
            }
        }

        return options;
    }
}
