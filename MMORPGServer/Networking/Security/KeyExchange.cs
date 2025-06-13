using System.Text;

namespace MMORPGServer.Networking.Security
{
    public static class KeyExchange
    {
        public static string Str_P = "A320A85EDD79171C341459E94807D71D39BB3B3F3B5161CA84894F3AC3FC7FEC317A2DDEC83B66D30C29261C6492643061AECFCF4A051816D7C359A6A7B7D8FB";
        public static string Str_G = "05";

        public static byte[] P = Array.Empty<byte>();
        public static byte[] G = Array.Empty<byte>();

        /// <summary>
        /// Creates the DH key byte arrays from hex strings - matching old implementation
        /// </summary>
        public static void CreateKeys()
        {
            // Convert hex strings to ASCII bytes like old implementation
            P = Encoding.ASCII.GetBytes(Str_P);
            G = Encoding.ASCII.GetBytes(Str_G);
        }

        /// <summary>
        /// Gets P parameter as byte array
        /// </summary>
        public static byte[] GetP()
        {
            if (P.Length == 0) CreateKeys();
            return P;
        }
        /// <summary>
        /// Gets G parameter as byte array
        /// </summary>
        public static byte[] GetG()
        {
            if (G.Length == 0) CreateKeys();
            return G;
        }
    }
}
