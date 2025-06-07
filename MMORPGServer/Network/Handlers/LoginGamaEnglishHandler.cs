namespace MMORPGServer.Network.Handlers
{
    public record LoginGamaEnglishData(uint Uid, uint State);
    public sealed class LoginGamaEnglishHandler : AbstractValidator<LoginGamaEnglishData>, IPacketProcessor
    {
        private readonly ILogger<LoginGamaEnglishHandler> _logger;

        public LoginGamaEnglishHandler(ILogger<LoginGamaEnglishHandler> logger)
        {
            _logger = logger;

            _ = RuleFor(x => x.Uid)
                .NotEmpty()
                .WithMessage("UID cannot be empty")
                .GreaterThan(1000000u)
                .WithMessage("UID must be greater than 0")
                .LessThanOrEqualTo(10000000u)
                .WithMessage("UID exceeds maximum allowed value");

            _ = RuleFor(x => x.State)
                .LessThanOrEqualTo(10U)
                .WithMessage("State value cannot exceed 10");

        }
        [PacketHandler(GamePackets.LoginGamaEnglish)]
        public async ValueTask HandleAsync(IGameClient client, Packet packet)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(packet);

            TransferCipher.Key = Encoding.ASCII.GetBytes("xBV1fH70fulyJyMapXdxWSnggELPwrPrRymW6jK93Wv9i79xUaSGR5Luzm9UCMhj");
            TransferCipher.Salt = Encoding.ASCII.GetBytes("z63b8u4NsNrHNFNPNeVB57tmt6gZQFfhz7hxr99HMqcpVQ3xSOYLJhX2b4PRzTXX");

            TransferCipher transferCipher = new("127.0.0.99");

            uint[] decrypted = new uint[2];

            _ = packet.GetReader().ReadEncrypted(transferCipher, out decrypted);

            LoginGamaEnglishData data = new(decrypted[0], decrypted[1]);

            var validationResult = await ValidateAsync(data);
            if (!validationResult.IsValid)
            {
                string errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning("Packet validation failed: {Errors}", errors);
                return;
            }
            _logger.LogInformation("LoginGamaEnglish decrypted UID: {uid}, State: {state}", data.Uid, data.State);
            await client.SendPacketAsync(TalkPacketHandler.CreateTalkPacket("SYSTEM", "ALLUSERS", "", "ANSWER_OK", ChatType.Dialog, 0));
        }
    }
    public partial class PacketFactory
    {
        public static ReadOnlyMemory<byte> CreateLoginGamaEnglish()
        {
            return PacketBuilder.Create(GamePackets.LoginGamaEnglish)
           .WriteUInt32(10002)
           .WriteUInt32(0)
           .Debug("CMsgLoginGame simple response")
           .BuildAndFinalize();
        }
    }
}
