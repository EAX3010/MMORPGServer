// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "<Pending>", Scope = "member", Target = "~M:MMORPGServer.Application.Game.World.GameWorld.SpawnPlayerAsync(MMORPGServer.Domain.Repositories.IGameClient,System.UInt16)~System.Threading.Tasks.Task{MMORPGServer.Domain.Entities.Player}")]
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "<Pending>", Scope = "member", Target = "~M:MMORPGServer.Infrastructure.Networking.Server.NetworkManager.AddClient(MMORPGServer.Domain.Repositories.IGameClient)")]
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "<Pending>", Scope = "member", Target = "~M:MMORPGServer.Infrastructure.Networking.Server.NetworkManager.BroadcastAsync(System.ReadOnlyMemory{System.Byte},System.UInt32)~System.Threading.Tasks.ValueTask")]
[assembly: SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "<Pending>", Scope = "member", Target = "~M:MMORPGServer.Infrastructure.Networking.Server.NetworkManager.RemoveClient(System.UInt32)")]
[assembly: SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "<Pending>", Scope = "member", Target = "~F:MMORPGServer.Application.Services.PlayerManager._players")]
[assembly: SuppressMessage("Minor Code Smell", "S1104:Fields should not have public accessibility", Justification = "<Pending>", Scope = "member", Target = "~F:MMORPGServer.Application.Services.PlayerManager._players")]
