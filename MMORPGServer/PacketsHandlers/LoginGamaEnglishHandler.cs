using FluentValidation;
using Microsoft.Extensions.Logging;
using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Services;

namespace MMORPGServer.PacketsHandlers
{
    public record LoginGamaEnglishData(int Id, int State);
    public sealed class LoginGamaEnglishHandler : AbstractValidator<LoginGamaEnglishData>,
        IPacketProcessor<GamePackets>
    {
        private readonly ILogger<LoginGamaEnglishHandler> _logger;
        private readonly IPacketFactory _packetFactory;
        private readonly ITransferCipher _transferCipher;
        private readonly PlayerManager _playerManager;
        private readonly IGameWorld _gameWorld;
        public LoginGamaEnglishHandler(ILogger<LoginGamaEnglishHandler> logger, IPacketFactory packetFactory, ITransferCipher transferCipher, PlayerManager playerManager, IGameWorld gameWorld)
        {
            _logger = logger;

            _ = RuleFor(x => x.Id)
                .NotEmpty()
                .WithMessage("UID cannot be empty")
                .GreaterThan(1000000)
                .WithMessage("UID must be greater than 0")
                .LessThanOrEqualTo(10000000)
                .WithMessage("UID exceeds maximum allowed value");

            _ = RuleFor(x => x.State)
                .LessThanOrEqualTo(10)
                .WithMessage("State value cannot exceed 10");
            _packetFactory = packetFactory;
            _transferCipher = transferCipher;
            _playerManager = playerManager;
            _gameWorld = gameWorld;
        }
        public GamePackets PacketType => GamePackets.LoginGamaEnglish;
        public async ValueTask HandleAsync(IGameClient client, IPacket packet)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(packet);


            uint[] outputDecrypted = _transferCipher.Decrypt([(uint)packet.ReadInt32(), (uint)packet.ReadInt32()]);

            LoginGamaEnglishData data = new((int)outputDecrypted[0], (int)outputDecrypted[1]);

            FluentValidation.Results.ValidationResult validationResult = await ValidateAsync(data);
            if (!validationResult.IsValid)
            {
                string errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning("Packet validation failed: {Errors}", errors);
                return;
            }
            _logger.LogInformation("LoginGamaEnglish decrypted UID: {uid}, State: {state}", data.Id, data.State);
            client.Player = await _playerManager.LoadPlayerAsync(data.Id, client.ClientId);
            if (client.Player != null)
            {
                await _gameWorld.SpawnPlayerAsync(client.Player, 1002);
                await client.SendPacketAsync(_packetFactory.CreateTalkPacket("SYSTEM", "ALLUSERS", "", "ANSWER_OK", ChatType.Dialog, 0));
                await client.SendPacketAsync(_packetFactory.CreateHeroInfoPacket(client.Player));
            }
            else
            {
                await client.SendPacketAsync(_packetFactory.CreateTalkPacket("SYSTEM", "ALLUSERS", "", "NEW_ROLE", ChatType.Dialog, 0));
            }
        }
    }

}
