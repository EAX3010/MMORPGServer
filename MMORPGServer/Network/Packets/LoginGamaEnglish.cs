using FluentValidation;
using MMORPGServer.Network.Fluent;

namespace MMORPGServer.Network.Packets
{
    public record LoginGamaEnglishData(uint Uid, uint State);
    public sealed class LoginGamaEnglish : AbstractValidator<LoginGamaEnglishData>, IPacketProcessor
    {
        private readonly ILogger<LoginGamaEnglish> _logger;

        public LoginGamaEnglish(ILogger<LoginGamaEnglish> logger)
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
        public async ValueTask LoginGamaEnglishHandler(IGameClient client, Packet packet)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(packet);

            TransferCipher.Key = Encoding.ASCII.GetBytes("xBV1fH70fulyJyMapXdxWSnggELPwrPrRymW6jK93Wv9i79xUaSGR5Luzm9UCMhj");
            TransferCipher.Salt = Encoding.ASCII.GetBytes("z63b8u4NsNrHNFNPNeVB57tmt6gZQFfhz7hxr99HMqcpVQ3xSOYLJhX2b4PRzTXX");

            var transferCipher = new TransferCipher("127.0.0.99");

            uint[] decrypted = new uint[2];

            _ = packet.GetReader().ReadEncrypted(transferCipher, out decrypted);

            LoginGamaEnglishData data = new(decrypted[0], decrypted[1]);

            var validationResult = await this.ValidateAsync(data);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning("Packet validation failed: {Errors}", errors);
                return;
            }


            _logger.LogInformation("LoginGamaEnglish decrypted UID: {uid}, State: {state}", data.Uid, data.State);
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
