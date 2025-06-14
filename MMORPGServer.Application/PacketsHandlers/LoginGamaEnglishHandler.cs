using FluentValidation;
using Microsoft.Extensions.Logging;
using MMORPGServer.Application.Interfaces;
using MMORPGServer.Domain.Enums;
using MMORPGServer.Domain.Interfaces;

namespace MMORPGServer.Application.PacketsHandlers
{
    public record LoginGamaEnglishData(uint Uid, uint State);
    public sealed class LoginGamaEnglishHandler : AbstractValidator<LoginGamaEnglishData>,
        IPacketProcessor<GamePackets>
    {
        private readonly ILogger<LoginGamaEnglishHandler> _logger;
        private readonly IPacketFactory _packetFactory;

        public LoginGamaEnglishHandler(ILogger<LoginGamaEnglishHandler> logger, IPacketFactory packetFactory)
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
            _packetFactory = packetFactory;
        }
        public GamePackets PacketType => GamePackets.LoginGamaEnglish;
        public async ValueTask HandleAsync(IGameClient client, IPacket packet)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(packet);

            // TransferCipher transferCipher =
            //   new("127.0.0.99",
            //  Encoding.ASCII.GetBytes("xBV1fH70fulyJyMapXdxWSnggELPwrPrRymW6jK93Wv9i79xUaSGR5Luzm9UCMhj"),
            //  Encoding.ASCII.GetBytes("z63b8u4NsNrHNFNPNeVB57tmt6gZQFfhz7hxr99HMqcpVQ3xSOYLJhX2b4PRzTXX"));

            //uint[] outputDecrypted = transferCipher.Decrypt([packet.ReadUInt32(), packet.ReadUInt32()]);

            LoginGamaEnglishData data = new(123123213, 21312321);

            FluentValidation.Results.ValidationResult validationResult = await ValidateAsync(data);
            if (!validationResult.IsValid)
            {
                string errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning("Packet validation failed: {Errors}", errors);
                return;
            }
            _logger.LogInformation("LoginGamaEnglish decrypted UID: {uid}, State: {state}", data.Uid, data.State);
            await client.SendPacketAsync(_packetFactory.CreateTalkPacket("SYSTEM", "ALLUSERS", "", "ANSWER_OK", ChatType.Dialog, 0));
            //await client.SendPacketAsync(_packetFactory.CreateHeroInfoPacket(client));
        }
    }

}
