using System.Numerics;

public sealed class DiffieHellmanKeyExchange : IDHKeyExchange
{
    private readonly BigInteger _p;
    private readonly BigInteger _g;
    private BigInteger _privateKey;
    private BigInteger _publicKey;
    private BigInteger _sharedSecret;

    public string SharedSecretHex => _sharedSecret.ToString("X");

    public DiffieHellmanKeyExchange()
    {
        // Initialize with Conquer Online standard parameters
        KeyExchange.CreateKeys();

        // Parse hex strings to BigInteger like the old implementation
        _p = BigInteger.Parse(KeyExchange.Str_P, System.Globalization.NumberStyles.HexNumber);
        _g = BigInteger.Parse(KeyExchange.Str_G, System.Globalization.NumberStyles.HexNumber);
    }

    public string GenerateRequest()
    {
        // Generate private key like old implementation - using similar method to genPseudoPrime
        _privateKey = GenerateRandomBigInteger(256);

        // Calculate A = g^a mod p
        _publicKey = BigInteger.ModPow(_g, _privateKey, _p);

        // Return as hex string like old implementation
        return _publicKey.ToString("X");
    }

    public void HandleResponse(string publicKey)
    {
        // Parse client's public key from hex string
        var clientPublicKey = BigInteger.Parse(publicKey, System.Globalization.NumberStyles.HexNumber);

        // Calculate shared secret: s = B^a mod p
        _sharedSecret = BigInteger.ModPow(clientPublicKey, _privateKey, _p);
    }

    public byte[] GetSharedSecret()
    {
        // Convert to byte array like old implementation's ToBytes() method
        var bytes = _sharedSecret.ToByteArray();

        // Handle BigInteger's little-endian format and potential extra zero byte
        if (bytes[bytes.Length - 1] == 0 && bytes.Length > 1)
        {
            Array.Resize(ref bytes, bytes.Length - 1);
        }

        // Reverse to match old implementation's byte order
        Array.Reverse(bytes);

        return bytes;
    }

    private static BigInteger GenerateRandomBigInteger(int bits)
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[bits / 8];
        rng.GetBytes(bytes);

        // Ensure positive number like old genPseudoPrime
        bytes[bytes.Length - 1] &= 0x7F;

        // Make sure it's not zero
        if (bytes.All(b => b == 0))
        {
            bytes[0] = 1;
        }

        return new BigInteger(bytes, isUnsigned: true);
    }

    /// <summary>
    /// Conquer Online DH Key Exchange Parameters
    /// </summary>
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