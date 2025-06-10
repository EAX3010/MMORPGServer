using MMORPGServer.Infrastructure.Networking.Fluent;
using System.Runtime.CompilerServices;

namespace MMORPGServer.Infrastructure.Networking
{
    /// <summary>
    /// Provides a fluent interface for building network packets with type safety and validation.
    /// </summary>
    public static class PacketBuilder
    {
        /// <summary>
        /// Creates a new packet writer for the specified packet type.
        /// </summary>
        /// <param name="type">The type of packet to create</param>
        /// <returns>A fluent packet writer interface</returns>
        public static IPacketWriter Create(GamePackets type)
        {
            ValidatePacketType(type);
            return new FluentPacketWriter(type);
        }

        /// <summary>
        /// Creates a new packet writer for the specified packet type.
        /// </summary>
        /// <param name="type">The type of packet to create</param>
        /// <returns>A fluent packet writer interface</returns>
        public static IPacketWriter Create(ushort type)
        {
            ValidatePacketType((GamePackets)type);
            return new FluentPacketWriter(type);
        }

        /// <summary>
        /// Validates that the packet type is defined in the GamePackets enum.
        /// </summary>
        /// <param name="type">The packet type to validate</param>
        /// <exception cref="ArgumentException">Thrown if the packet type is not defined</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ValidatePacketType(GamePackets type)
        {
            if (!Enum.IsDefined(typeof(GamePackets), type))
            {
                throw new ArgumentException($"Invalid packet type: {type}", nameof(type));
            }
        }

        /// <summary>
        /// Creates a packet for sending a simple message.
        /// </summary>
        /// <param name="type">The packet type</param>
        /// <param name="message">The message to send</param>
        /// <returns>A fluent packet writer interface</returns>
        public static IPacketWriter CreateMessage(GamePackets type, string message)
        {
            return Create(type)
                .WriteString(message, 256)
                .Debug($"Creating message packet: {message}");
        }

        /// <summary>
        /// Creates a packet for sending a position update.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="facing">Direction facing</param>
        /// <returns>A fluent packet writer interface</returns>
        public static IPacketWriter CreatePositionUpdate(ushort x, ushort y, ushort facing)
        {
            return Create(GamePackets.CMsgSyncAction)
                .WriteUInt16(x)
                .WriteUInt16(y)
                .WriteUInt16(facing)
                .Debug($"Creating position update: ({x}, {y}) facing {facing}");
        }

        /// <summary>
        /// Creates a packet for sending an action.
        /// </summary>
        /// <param name="uid">Entity UID</param>
        /// <param name="actionType">Type of action</param>
        /// <param name="param1">First parameter</param>
        /// <param name="param2">Second parameter</param>
        /// <returns>A fluent packet writer interface</returns>
        public static IPacketWriter CreateAction(uint uid, ushort actionType, ushort param1, ushort param2)
        {
            return Create(GamePackets.CMsgAction)
                .WriteUInt32(uid)
                .WriteUInt16(actionType)
                .WriteUInt16(param1)
                .WriteUInt16(param2)
                .Debug($"Creating action: UID={uid}, Type={actionType}, Params=({param1}, {param2})");
        }
    }
}
