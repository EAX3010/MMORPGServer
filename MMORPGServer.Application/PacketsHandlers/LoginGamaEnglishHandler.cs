using FluentValidation;
using Microsoft.Extensions.Logging;
using MMORPGServer.Application.Interfaces;
using MMORPGServer.Application.Services;
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
        private readonly ITransferCipher _transferCipher;
        private readonly IPlayerManager _playerManager;
        private readonly GameWorld _gameWorld;

        public LoginGamaEnglishHandler(ILogger<LoginGamaEnglishHandler> logger, IPacketFactory packetFactory, ITransferCipher transferCipher, IPlayerManager playerManager, GameWorld gameWorld)
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
            _transferCipher = transferCipher;
            _playerManager = playerManager;
            _gameWorld = gameWorld;
        }
        public GamePackets PacketType => GamePackets.LoginGamaEnglish;
        public async ValueTask HandleAsync(IGameClient client, IPacket packet)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(packet);


            uint[] outputDecrypted = _transferCipher.Decrypt([packet.ReadUInt32(), packet.ReadUInt32()]);

            LoginGamaEnglishData data = new(outputDecrypted[0], outputDecrypted[1]);

            FluentValidation.Results.ValidationResult validationResult = await ValidateAsync(data);
            if (!validationResult.IsValid)
            {
                string errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                _logger.LogWarning("Packet validation failed: {Errors}", errors);
                return;
            }
            _logger.LogInformation("LoginGamaEnglish decrypted UID: {uid}, State: {state}", data.Uid, data.State);
            client.Player = new Domain.Entities.Player(client.ClientId, data.Uid);
            await _playerManager.AddPlayerAsync(client.Player);
            await _gameWorld.SpawnPlayerAsync(client.Player, 1002);
            await client.SendPacketAsync(_packetFactory.CreateTalkPacket("SYSTEM", "ALLUSERS", "", "ANSWER_OK", ChatType.Dialog, 0));
            await client.SendPacketAsync(_packetFactory.CreateHeroInfoPacket(client.Player));
        }
    }

}
