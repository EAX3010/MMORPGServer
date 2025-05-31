namespace MMORPGServer.Game
{
    public sealed class PacketProcessor : IPacketProcessor
    {
        private readonly ILogger<PacketProcessor> _logger;
        private readonly IPlayerManager _playerManager;
        private readonly IAuthenticationService _authService;
        private readonly ICharacterService _characterService;
        private readonly IChatService _chatService;

        public PacketProcessor(
            ILogger<PacketProcessor> logger,
            IPlayerManager playerManager,
            IAuthenticationService authService,
            ICharacterService characterService,
            IChatService chatService)
        {
            _logger = logger;
            _playerManager = playerManager;
            _authService = authService;
            _characterService = characterService;
            _chatService = chatService;
        }

        public async ValueTask ProcessPacketAsync(IGameClient client, ConquerPacket packet)
        {
            try
            {
                switch (packet.Type)
                {
                    case PacketTypes.LOGIN_REQUEST:
                        await HandleLoginRequestAsync(client, packet);
                        break;

                    case PacketTypes.CHARACTER_LIST_REQUEST:
                        await HandleCharacterListRequestAsync(client, packet);
                        break;

                    case PacketTypes.CHARACTER_SELECT:
                        await HandleCharacterSelectAsync(client, packet);
                        break;

                    case PacketTypes.MOVEMENT_REQUEST:
                        await HandleMovementRequestAsync(client, packet);
                        break;

                    case PacketTypes.CHAT_MESSAGE:
                        await HandleChatMessageAsync(client, packet);
                        break;

                    case PacketTypes.ATTACK_REQUEST:
                        await HandleAttackRequestAsync(client, packet);
                        break;

                    default:
                        _logger.LogWarning("Unknown packet type: {PacketType} from client {ClientId}",
                            packet.Type, client.ClientId);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing packet {PacketType} from client {ClientId}",
                    packet.Type, client.ClientId);
            }
        }

        public void RegisterPacketHandler(ushort packetType, Func<IGameClient, ConquerPacket, ValueTask> handler)
        {
            _logger.LogDebug("Registered handler for packet type {PacketType}", packetType);
        }

        private async ValueTask HandleLoginRequestAsync(IGameClient client, ConquerPacket packet)
        {
            var reader = packet.CreateReader();
            var username = reader.ReadString(16);
            var password = reader.ReadString(16);

            _logger.LogInformation("Login request from client {ClientId}: {Username}",
                client.ClientId, username);

            var loginResult = await _authService.AuthenticateAsync(username, password);

            using var response = new ConquerPacket(PacketTypes.LOGIN_RESPONSE);
            var writer = response.CreateWriter();
            writer.WriteUInt32(loginResult.Success ? 1u : 0u);
            writer.WriteUInt32(loginResult.UserId);

            await client.SendPacketAsync(writer.ToPacket());
        }

        private async ValueTask HandleCharacterListRequestAsync(IGameClient client, ConquerPacket packet)
        {
            // TODO: Get user ID from session
            var characters = await _characterService.GetCharactersByUserIdAsync(12345);

            using var response = new ConquerPacket(PacketTypes.CHARACTER_LIST_RESPONSE);
            var writer = response.CreateWriter();

            writer.WriteUInt32((uint)characters.Count);

            foreach (var character in characters)
            {
                writer.WriteUInt32(character.CharacterId);
                writer.WriteString(character.Name, 16);
                writer.WriteUInt16(character.Level);
                writer.WriteUInt16(character.Class);
                writer.WriteUInt32(character.MapId);
                writer.WriteUInt16(character.X);
                writer.WriteUInt16(character.Y);
            }

            await client.SendPacketAsync(writer.ToPacket());
        }

        private async ValueTask HandleCharacterSelectAsync(IGameClient client, ConquerPacket packet)
        {
            var reader = packet.CreateReader();
            var characterId = reader.ReadUInt32();

            _logger.LogInformation("Character select request from client {ClientId}: {CharacterId}",
                client.ClientId, characterId);

            var character = await _characterService.GetCharacterAsync(characterId);
            if (character != null)
            {
                // TODO: Create Player instance and assign to client
                // client.Player = new Player { ... };

                using var response = new ConquerPacket(0x9C4); // Character login success
                var writer = response.CreateWriter();
                writer.WriteUInt32(characterId);
                writer.WriteUInt32(character.MapId);
                writer.WriteUInt16(character.X);
                writer.WriteUInt16(character.Y);

                await client.SendPacketAsync(writer.ToPacket());
            }
        }

        private async ValueTask HandleMovementRequestAsync(IGameClient client, ConquerPacket packet)
        {
            if (client.Player is null) return;

            var reader = packet.CreateReader();
            var x = reader.ReadUInt16();
            var y = reader.ReadUInt16();
            var direction = reader.ReadByte();

            await client.Player.MoveToAsync(x, y, direction);
        }

        private async ValueTask HandleChatMessageAsync(IGameClient client, ConquerPacket packet)
        {
            if (client.Player is null) return;

            var reader = packet.CreateReader();
            var chatType = reader.ReadByte();
            var message = reader.ReadString(255);

            await _chatService.BroadcastMessageAsync(client.Player.CharacterId, message);
        }

        private async ValueTask HandleAttackRequestAsync(IGameClient client, ConquerPacket packet)
        {
            if (client.Player is null) return;

            var reader = packet.CreateReader();
            var targetId = reader.ReadUInt32();
            var attackType = reader.ReadUInt16();

            _logger.LogDebug("Attack request from {PlayerId} to {TargetId}",
                client.Player.CharacterId, targetId);

            // TODO: Implement combat system
            await ValueTask.CompletedTask;
        }
    }
}