using MMORPGServer.Domain.Interfaces;
using MMORPGServer.Infrastructure.Networking.Packets;
using System.Security.Cryptography;
using System.Text;

namespace MMORPGServer.Infrastructure.Networking.Security
{
    /// <summary>
    /// Handles Diffie-Hellman key exchange for secure communication.
    /// </summary>
    public sealed class DiffieHellmanKeyExchange : IDisposable
    {
        #region Private Fields
        private readonly BigInteger _prime;
        private readonly BigInteger _generator;
        private BigInteger _privateKey;
        private BigInteger _publicKey;
        private BigInteger _sharedSecret;
        private bool _disposed;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the shared secret as a BigInteger.
        /// </summary>
        public BigInteger SharedSecret => _sharedSecret;

        /// <summary>
        /// Gets the public key for transmission.
        /// </summary>
        public BigInteger PublicKey => _publicKey;

        /// <summary>
        /// Gets the shared secret as a hex string.
        /// </summary>
        public string SharedSecretHex => _sharedSecret.ToHexString();

        /// <summary>
        /// Gets whether the key exchange has been completed.
        /// </summary>
        public bool IsComplete => _sharedSecret != 0;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new Diffie-Hellman key exchange with predefined parameters.
        /// </summary>
        public DiffieHellmanKeyExchange()
        {
            _prime = new BigInteger(KeyExchange.Str_P, 16);
            _generator = new BigInteger(KeyExchange.Str_G, 16);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Generates the server's public key for the initial request.
        /// </summary>
        /// <returns>The server's public key as a hex string.</returns>
        public string GenerateServerRequest()
        {
            _privateKey = BigInteger.genPseudoPrime(256, 30, new Random());
            _publicKey = _generator.modPow(_privateKey, _prime);
            return _publicKey.ToHexString();
        }

        /// <summary>
        /// Generates the client's response and computes the shared secret.
        /// </summary>
        /// <param name="serverPublicKey">The server's public key as a hex string.</param>
        /// <returns>The client's public key as a hex string.</returns>
        public string GenerateClientResponse(string serverPublicKey)
        {
            if (string.IsNullOrEmpty(serverPublicKey))
                throw new ArgumentException("Server public key cannot be null or empty.", nameof(serverPublicKey));

            _privateKey = BigInteger.genPseudoPrime(256, 30, new Random());
            _publicKey = _generator.modPow(_privateKey, _prime);

            BigInteger serverKey = new BigInteger(serverPublicKey, 16);
            _sharedSecret = serverKey.modPow(_privateKey, _prime);

            return _publicKey.ToHexString();
        }

        /// <summary>
        /// Handles the client's response to complete the key exchange on the server side.
        /// </summary>
        /// <param name="clientPublicKey">The client's public key as a hex string.</param>
        public void HandleClientResponse(string clientPublicKey)
        {
            if (string.IsNullOrEmpty(clientPublicKey))
                throw new ArgumentException("Client public key cannot be null or empty.", nameof(clientPublicKey));

            if (_privateKey == 0)
                throw new InvalidOperationException("Server request must be generated first.");

            BigInteger clientKey = new BigInteger(clientPublicKey, 16);
            _sharedSecret = clientKey.modPow(_privateKey, _prime);
        }

        /// <summary>
        /// Derives the final encryption key from the shared secret.
        /// </summary>
        /// <returns>The derived key as a byte array.</returns>
        public byte[] DeriveEncryptionKey()
        {
            if (!IsComplete)
                throw new InvalidOperationException("Key exchange must be completed first.");

            using MD5 md5 = MD5.Create();

            byte[] secretBytes = _sharedSecret.getBytes();
            int validLength = GetValidKeyLength(secretBytes);

            // First hash: MD5(shared_secret)
            byte[] firstHash = md5.ComputeHash(secretBytes, 0, validLength);
            string firstHex = ConvertToHex(firstHash);

            // Second hash: MD5(firstHex + firstHex)
            string concatenated = firstHex + firstHex;
            byte[] concatenatedBytes = Encoding.ASCII.GetBytes(concatenated);
            byte[] secondHash = md5.ComputeHash(concatenatedBytes);
            string secondHex = ConvertToHex(secondHash);

            // Final result: firstHex + secondHex
            string finalKey = firstHex + secondHex;
            return ConvertStringToByteArray(finalKey);
        }

        /// <summary>
        /// Creates a Diffie-Hellman key exchange packet for network transmission.
        /// </summary>
        /// <returns>The packet data ready for transmission.</returns>
        public ReadOnlyMemory<byte> CreateKeyExchangePacket()
        {
            string publicKeyHex = GenerateServerRequest();
            byte[] publicKeyBytes = Encoding.ASCII.GetBytes(publicKeyHex);

            using Packet packet = new Packet((ushort)0, isServerPacket: true, capacity: 1024);

            byte[] pBytes = KeyExchange.GetP();
            byte[] gBytes = KeyExchange.GetG();
            uint totalSize = CalculatePacketSize(pBytes.Length, gBytes.Length, publicKeyBytes.Length);

            WritePacketHeader(packet, totalSize);
            WritePacketData(packet, pBytes, gBytes, publicKeyBytes);
            packet.WriteSeal();

            return packet.GetFinalizedMemory();
        }

        /// <summary>
        /// Gets the shared secret as raw bytes.
        /// </summary>
        /// <returns>The shared secret as a byte array.</returns>
        public byte[] GetSharedSecretBytes()
        {
            if (!IsComplete)
                throw new InvalidOperationException("Key exchange must be completed first.");

            return _sharedSecret.getBytes();
        }
        #endregion

        #region Private Helper Methods
        private static string ConvertToHex(byte[] bytes)
        {
            char[] result = new char[bytes.Length * 2];

            for (int i = 0, j = 0; i < bytes.Length; i++, j += 2)
            {
                byte highNibble = (byte)(bytes[i] >> 4);
                byte lowNibble = (byte)(bytes[i] & 0x0F);

                result[j] = (char)(highNibble > 9 ? highNibble + 0x57 : highNibble + 0x30);
                result[j + 1] = (char)(lowNibble > 9 ? lowNibble + 0x57 : lowNibble + 0x30);
            }

            return new string(result);
        }

        private static byte[] ConvertStringToByteArray(string input)
        {
            byte[] result = new byte[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                result[i] = (byte)input[i];
            }
            return result;
        }

        private static int GetValidKeyLength(byte[] keyBytes)
        {
            for (int i = 0; i < keyBytes.Length; i++)
            {
                if (keyBytes[i] == 0)
                    return i;
            }
            return keyBytes.Length;
        }

        private static uint CalculatePacketSize(int pLength, int gLength, int publicKeyLength)
        {
            return (uint)(75 + pLength + gLength + publicKeyLength);
        }

        private static void WritePacketHeader(IPacket packet, uint totalSize)
        {
            packet.Seek(11);
            packet.WriteUInt32(totalSize - 11);

            // Write placeholder sections
            packet.WriteUInt32(10);
            packet.Skip(10);
            packet.WriteUInt32(8);
            packet.Skip(8);
            packet.WriteUInt32(8);
            packet.Skip(8);
        }

        private static void WritePacketData(IPacket packet, byte[] pBytes, byte[] gBytes, byte[] publicKeyBytes)
        {
            // Write P parameter
            packet.WriteUInt32((uint)pBytes.Length);
            packet.WriteBytes(pBytes);

            // Write G parameter
            packet.WriteUInt32((uint)gBytes.Length);
            packet.WriteBytes(gBytes);

            // Write public key
            packet.WriteUInt32((uint)publicKeyBytes.Length);
            packet.WriteBytes(publicKeyBytes);

            packet.Skip(2); // Padding
        }
        #endregion
        #region ToString Override
        public override string ToString() => SharedSecretHex;
        #endregion

        #region IDisposable Implementation
        public void Dispose()
        {
            if (_disposed) return;

            // Clear sensitive data
            _privateKey = 0;
            _sharedSecret = 0;
            _publicKey = 0;

            _disposed = true;
        }
        #endregion
    }
}