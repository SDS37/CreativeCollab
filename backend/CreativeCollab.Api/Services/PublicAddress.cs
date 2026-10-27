using System.Net;
using System.Net.Sockets;

namespace CreativeCollab.Api.Services;

/// <summary>Decides whether an address is safe for the server to call on a Collaborator's behalf.</summary>
public static class PublicAddress
{
    public static bool IsPublic(IPAddress address) =>
        address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicV4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsPublicV6(address),
            _ => false,
        };

    private static bool IsPublicV4(byte[] b) =>
        !(b[0] == 0                                          // 0.0.0.0/8 "this network"
          || b[0] == 10                                      // private
          || (b[0] == 100 && (b[1] & 0xC0) == 64)            // 100.64.0.0/10 carrier-grade NAT
          || b[0] == 127                                     // loopback
          || (b[0] == 169 && b[1] == 254)                    // link-local, includes cloud metadata
          || (b[0] == 172 && (b[1] & 0xF0) == 16)            // private
          || (b[0] == 192 && b[1] == 0 && b[2] == 0)         // IETF protocol assignments
          || (b[0] == 192 && b[1] == 0 && b[2] == 2)         // documentation
          || (b[0] == 192 && b[1] == 168)                    // private
          || (b[0] == 198 && (b[1] & 0xFE) == 18)            // benchmarking
          || (b[0] == 198 && b[1] == 51 && b[2] == 100)      // documentation
          || (b[0] == 203 && b[1] == 0 && b[2] == 113)       // documentation
          || b[0] >= 224);                                   // multicast, reserved, broadcast

    private static bool IsPublicV6(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            return IsPublicV4(address.MapToIPv4().GetAddressBytes());

        var b = address.GetAddressBytes();

        // ::/96 covers unspecified, loopback and the deprecated IPv4-compatible form.
        if (b[..12].All(x => x == 0))
            return false;

        // 64:ff9b::/96 (NAT64) and 2002::/16 (6to4) embed an IPv4 address that must itself be public.
        if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B && b[4..12].All(x => x == 0))
            return IsPublicV4(b[12..16]);
        if (b[0] == 0x20 && b[1] == 0x02)
            return IsPublicV4(b[2..6]);

        return !((b[0] & 0xFE) == 0xFC                                  // fc00::/7 unique-local
                 || (b[0] == 0xFE && (b[1] & 0xC0) == 0x80)             // fe80::/10 link-local
                 || (b[0] == 0xFE && (b[1] & 0xC0) == 0xC0)             // fec0::/10 site-local
                 || b[0] == 0xFF                                        // multicast
                 || (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B) // other NAT64 ranges
                 || (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x00 && b[3] == 0x00) // Teredo
                 || (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8)); // documentation
    }
}
