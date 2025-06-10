using FluentValidation;
using Microsoft.Extensions.Logging;
using MMORPGServer.Domain.Enums;
using MMORPGServer.Domain.Repositories;
using MMORPGServer.Infrastructure.Security;
using MMORPGServer.Network.Attributes;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public async ValueTask HandleAsync(IGameClient client, IPacket packet)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(packet);

            TransferCipher.Key = Encoding.ASCII.GetBytes("xBV1fH70fulyJyMapXdxWSnggELPwrPrRymW6jK93Wv9i79xUaSGR5Luzm9UCMhj");
            TransferCipher.Salt = Encoding.ASCII.GetBytes("z63b8u4NsNrHNFNPNeVB57tmt6gZQFfhz7hxr99HMqcpVQ3xSOYLJhX2b4PRzTXX");

            TransferCipher transferCipher = new("127.0.0.99");

            var outputDecrypted = transferCipher.Decrypt([packet.ReadUInt32(), packet.ReadUInt32()]);

            LoginGamaEnglishData data = new(outputDecrypted[0], outputDecrypted[1]);

            var validationResult = await ValidateAsync(data);
            if (!validationResult.IsValid)
            {
                string errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning("Packet validation failed: {Errors}", errors);
                return;
            }
            _logger.LogInformation("LoginGamaEnglish decrypted UID: {uid}, State: {state}", data.Uid, data.State);
            await client.SendPacketAsync(PacketFactory.CreateTalkPacket("SYSTEM", "ALLUSERS", "", "ANSWER_OK", ChatType.Dialog, 0));
            await client.SendPacketAsync(PacketFactory.CreateHeroInfoPacket(client.Player));
        }
    }

}
