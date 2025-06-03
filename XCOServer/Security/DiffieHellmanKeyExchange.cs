namespace MMORPGServer.Security
{
    public sealed class DiffieHellmanKeyExchange : IDHKeyExchange
    {

        private BigInteger p = 0;
        private BigInteger g = 0;
        private BigInteger a = 0;
        private BigInteger b = 0;
        private BigInteger s = 0;
        private BigInteger A = 0;
        private BigInteger B = 0;

        public BigInteger GetKey() => s;
        public BigInteger GetRequest() => A;
        public BigInteger GetResponse() => A;

        public String Key { get { return s.ToHexString(); } }

        public override String ToString() => s.ToHexString();
        public Byte[] ToBytes() => s.getBytes();

        /// <summary>
        /// Create a new Diffie-Hellman exchange where the prime number is p and the base is g.
        /// </summary>
        public DiffieHellmanKeyExchange()
        {
            this.p = new BigInteger(KeyExchange.Str_P, 16);
            this.g = new BigInteger(KeyExchange.Str_G, 16);
        }

        /// <summary>
        /// Generates the server request and return the A key.
        /// </summary>
        public String GenerateRequest()
        {
            a = BigInteger.genPseudoPrime(256, 30, new Random());
            A = g.modPow(a, p);

            return A.ToHexString();
        }

        /// <summary>
        /// Generates the client response and the S key with the A key.
        /// The B key will be returned.
        /// </summary>
        public String GenerateResponse(String PubKey)
        {
            b = BigInteger.genPseudoPrime(256, 30, new Random());
            B = g.modPow(b, p);

            A = new BigInteger(PubKey, 16);
            s = A.modPow(b, p);

            return B.ToHexString();
        }

        /// <summary>
        /// Handles the client response to generate the S key with the B key.
        /// </summary>
        public void HandleResponse(String PubKey)
        {
            B = new BigInteger(PubKey, 16);
            s = B.modPow(a, p);
        }

        private string Hex(byte[] bytes)
        {
            char[] c = new char[bytes.Length * 2];
            byte b;
            for (int bx = 0, cx = 0; bx < bytes.Length; ++bx, ++cx)
            {
                b = ((byte)(bytes[bx] >> 4));
                c[cx] = (char)(b > 9 ? b + 0x37 + 0x20 : b + 0x30);
                b = ((byte)(bytes[bx] & 0x0F));
                c[++cx] = (char)(b > 9 ? b + 0x37 + 0x20 : b + 0x30);
            }
            return new string(c);
        }
        public byte[] GetSecret()
        {
            var hashService = new System.Security.Cryptography.MD5CryptoServiceProvider();
            var s1 = Hex(hashService.ComputeHash(this.s.getBytes(), 0, FixKey(this.s.getBytes())));//key.TakeWhile<byte>(((Func<byte, bool>)(x => (x != 0)))).Count<byte>()));
            var s2 = Hex(hashService.ComputeHash(Encoding.ASCII.GetBytes(String.Concat(s1, s1))));
            var sresult = String.Concat(s1, s2);

            return GetArrayPostProcessDHKey(sresult);
        }
        public byte[] GetArrayPostProcessDHKey(string sresult)
        {
            byte[] skey = new byte[sresult.Length];
            for (int x = 0; x < sresult.Length; x++)
                skey[x] = (byte)sresult[x];
            return skey;
        }
        public int FixKey(byte[] key)
        {
            for (int x = 0; x < key.Length; x++)
            {
                if (key[x] == 0)
                    return x;
            }
            return key.Length;
        }
        public Packet CreateDHKeyPacket()
        {
            var publicKey = GenerateRequest();
            var publicKeyBytes = Encoding.ASCII.GetBytes(publicKey);
            using var packet = new Packet(0, true, 1024);
            var pBytes = KeyExchange.GetP();
            var gBytes = KeyExchange.GetG();
            uint size = (uint)(75 + pBytes.Length + gBytes.Length + publicKey.Length);
            packet.Seek(11);
            packet.WriteUInt32(size - 11);
            packet.WriteUInt32(10);
            packet.Skip(10);
            packet.WriteUInt32(8);
            packet.Skip(8);
            packet.WriteUInt32(8);
            packet.Skip(8);
            packet.WriteUInt32((uint)pBytes.Length);
            packet.WriteBytes(pBytes);
            packet.WriteUInt32((uint)gBytes.Length);
            packet.WriteBytes(gBytes);
            packet.WriteUInt32((uint)publicKeyBytes.Length);
            packet.WriteBytes(publicKeyBytes);
            packet.Skip(2);
            packet.WriteSeal(true);
            packet.GetFinalizedMemory();
            return packet;
        }

    }
}