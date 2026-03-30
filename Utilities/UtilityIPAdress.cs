using System.Net;
using System.Net.Sockets;

public static class UtilityIPAdress
{
    /// <summary> Check is IPv4-address valid?  </summary>
    public static bool IsIPv4Valid(string IPv4)
    {
        if (string.IsNullOrWhiteSpace(IPv4))
            return false;

        if (IPAddress.TryParse(IPv4, out IPAddress address))
        {
            // Check IPv4 (not IPv6)
            if (address.AddressFamily != AddressFamily.InterNetwork)
                return false;

            // Check format
            string[] parts = IPv4.Split('.');
            if (parts.Length != 4)
                return false;

            // Check separately parts
            foreach (string part in parts)
                if (!byte.TryParse(part, out _))
                    return false;

            return true;
        }

        return false;
    }
}