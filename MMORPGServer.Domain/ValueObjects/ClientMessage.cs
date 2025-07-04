﻿using MMORPGServer.Domain.Common.Interfaces;

namespace MMORPGServer.Domain.ValueObjects
{
    public readonly record struct ClientMessage(IGameClient Client, IPacket Packet);
}
