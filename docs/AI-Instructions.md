# 🎮 AI Instructions: Conquer Game Server Architecture

> **For AI Assistants**: Use these instructions when helping with Conquer game server development. This defines the exact architecture patterns, structure, and conventions to follow.

---

## 🎯 **Project Context**

**Game Type**: Conquer Online - Classic MMORPG  
**Architecture**: Clean Architecture + Repository Pattern + Domain-Driven Design (DDD)  
**Platform**: .NET 9 Console Application (NOT Web API)  
**Protocol**: Custom TCP Binary Protocol  
**Database**: SQL Server with Entity Framework Core  

---

## 📁 **Project Structure (MANDATORY)**

```
ConquerServer/
├── src/
│   ├── ConquerServer.Domain/              # 🎮 Core game business logic
│   ├── ConquerServer.Application/         # 🎭 Use cases & orchestration
│   ├── ConquerServer.Infrastructure/      # 🔧 Technical implementations
│   ├── ConquerServer.Presentation.Network/   # 🖥️ TCP game protocol
│   ├── ConquerServer.BackgroundServices/  # 🔄 Game loop & services
│   └── ConquerServer.Host/                # 🚀 Console application startup
├── tests/ (optional)
├── docs/
├── README.md
├── .gitignore
└── ConquerServer.sln
```

**⚠️ IMPORTANT**: This is a **Console Application**, NOT a web API. No ASP.NET Core, no REST endpoints, no HTTP.

---

## 🏛️ **Layer Responsibilities**

### **🎮 Domain Layer** (`ConquerServer.Domain`)
**Purpose**: Pure Conquer game business logic with zero external dependencies

**Contains**:
- **Entities**: Player, Map, Item, Guild, Monster, NPC
- **ValueObjects**: Position, ItemType, PlayerClass, SkillType
- **Services**: CombatService, MovementService, ItemService, SkillService
- **Repositories**: IPlayerRepository, IMapRepository, IItemRepository (interfaces only!)
- **Events**: PlayerLevelUpEvent, PlayerDeathEvent, ItemDropEvent
- **Exceptions**: ConquerDomainException, InvalidMoveException

**Rules**:
- ✅ NO external dependencies (no EF, no TCP, no file I/O)
- ✅ Contains ALL Conquer game rules and validation
- ✅ Rich domain models with behavior
- ✅ Use interfaces for external needs

### **🎭 Application Layer** (`ConquerServer.Application`)
**Purpose**: Orchestrates Domain services for Conquer-specific use cases

**Contains**:
- **Commands**: MovePlayerCommand, AttackCommand, UseItemCommand
- **Queries**: GetPlayerQuery, GetMapQuery, GetInventoryQuery
- **Handlers**: MovePlayerHandler, AttackHandler, ItemHandler
- **Services**: GameSessionService, PlayerService

**Rules**:
- ✅ Coordinates multiple Domain services
- ✅ NO business logic (delegate to Domain)
- ✅ Handles transactions and cross-cutting concerns

### **🔧 Infrastructure Layer** (`ConquerServer.Infrastructure`)
**Purpose**: Implements Domain interfaces with concrete Conquer-specific technology

**Contains**:
- **Persistence**: SqlPlayerRepository, SqlMapRepository, ConquerDbContext
- **Network**: TcpGameServer, ConquerClient, PacketSerializer
- **Security**: ConquerEncryption, BlowfishCipher, GameSecurity
- **External**: FileMapLoader, ConfigurationService

**Rules**:
- ✅ Implements ALL Domain repository interfaces
- ✅ Contains Conquer-specific network protocol
- ✅ Handles database operations and file I/O

### **🖥️ Presentation.Network Layer** (`ConquerServer.Presentation.Network`)
**Purpose**: Handles Conquer TCP protocol and packet processing

**Contains**:
- **Handlers**: LoginPacketHandler, MovePacketHandler, AttackPacketHandler
- **Protocols**: ConquerPackets, PacketType enum, PacketProcessor
- **Services**: NetworkService, ClientManager
- **Middleware**: AuthenticationMiddleware, RateLimitingMiddleware

**Rules**:
- ✅ Handles ALL Conquer packet types
- ✅ Converts packets to Application commands
- ✅ Manages client connections and protocol state

### **🔄 BackgroundServices Layer** (`ConquerServer.BackgroundServices`)
**Purpose**: Conquer-specific background processes

**Contains**:
- **GameLoopService**: Main game tick (handles combat, movement, spawning)
- **NetworkListenerService**: TCP connection acceptance
- **MonsterAIService**: NPC and monster behavior
- **ItemDecayService**: Item cleanup and respawning
- **GuildWarService**: Guild war mechanics
- **SaveService**: Periodic player data saves

### **🚀 Host Layer** (`ConquerServer.Host`)
**Purpose**: Console application entry point and DI setup

**Contains**:
- **Program.cs**: Console application main entry
- **Configuration**: Dependency injection setup
- **appsettings.json**: Conquer server configuration

---

## 🎮 **Conquer-Specific Domain Concepts**

### **Core Entities**:
```csharp
// Domain/Entities/Player.cs
public class Player 
{
    public uint Id { get; }
    public string Name { get; }
    public PlayerClass Class { get; }
    public Position Position { get; }
    public uint Level { get; }
    public uint Experience { get; }
    public uint Money { get; }
    public PlayerStatus Status { get; }
    
    // Conquer-specific methods
    public bool CanAttack(Player target);
    public void GainExperience(uint amount);
    public void TakeDamage(uint damage);
}
```

### **Value Objects**:
```csharp
// Domain/ValueObjects/Position.cs
public readonly record struct Position(ushort X, ushort Y, ushort MapId);

// Domain/ValueObjects/PlayerClass.cs
public enum PlayerClass : byte
{
    Trojan = 10,
    Warrior = 20, 
    Archer = 40,
    Ninja = 50,
    Monk = 60,
    Pirate = 70
}
```

### **Domain Services**:
```csharp
// Domain/Services/CombatService.cs
public class CombatService
{
    public AttackResult ProcessAttack(Player attacker, Player target);
    public uint CalculateDamage(Player attacker, Player target);
    public bool IsInAttackRange(Player attacker, Player target);
}
```

---

## 🔌 **Dependency Injection Pattern**

### **Program.cs Structure**:
```csharp
// Host/Program.cs
using ConquerServer.Application.Extensions;
using ConquerServer.Infrastructure.Extensions;
using ConquerServer.BackgroundServices.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Register services
builder.Services.AddDomainServices();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddBackgroundServices();

var host = builder.Build();
await host.RunAsync();
```

### **Service Registration Extensions**:
```csharp
// Application/Extensions/ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddScoped<CombatService>();
        services.AddScoped<MovementService>();
        services.AddScoped<ItemService>();
        return services;
    }
    
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<MovePlayerHandler>();
        services.AddScoped<AttackHandler>();
        services.AddScoped<ItemHandler>();
        return services;
    }
}
```

---

## 🌐 **Network Protocol Handling**

### **Conquer Packet Structure**:
```csharp
// Presentation.Network/Protocols/ConquerPacket.cs
public abstract class ConquerPacket
{
    public ushort Size { get; set; }
    public ushort Type { get; set; }
    
    public abstract byte[] Encode();
    public abstract void Decode(byte[] buffer);
}

// Specific packet types
public class MsgAction : ConquerPacket // Movement, attacks
public class MsgTalk : ConquerPacket   // Chat messages  
public class MsgItem : ConquerPacket   // Item operations
public class MsgUserInfo : ConquerPacket // Player info
```

### **Packet Handling Pattern**:
```csharp
// Presentation.Network/Handlers/MovePacketHandler.cs
public class MovePacketHandler
{
    private readonly MovePlayerHandler _moveHandler;
    
    public async Task HandleAsync(ConquerClient client, MsgAction packet)
    {
        var command = new MovePlayerCommand(client.PlayerId, packet.X, packet.Y);
        var result = await _moveHandler.HandleAsync(command);
        
        if (result.Success)
            await client.SendAsync(new MsgAction { /* response */ });
    }
}
```

---

## 📋 **Naming Conventions**

### **Project Names**:
- `ConquerServer.Domain`
- `ConquerServer.Application` 
- `ConquerServer.Infrastructure`
- `ConquerServer.Presentation.Network`
- `ConquerServer.BackgroundServices`
- `ConquerServer.Host`

### **File Naming**:
- **Entities**: `Player.cs`, `Map.cs`, `Item.cs`
- **Commands**: `MovePlayerCommand.cs`, `AttackCommand.cs`
- **Handlers**: `MovePlayerHandler.cs`, `AttackHandler.cs`
- **Packets**: `MsgAction.cs`, `MsgTalk.cs`, `MsgItem.cs`
- **Services**: `CombatService.cs`, `MovementService.cs`

### **Namespace Convention**:
```csharp
namespace ConquerServer.Domain.Entities;
namespace ConquerServer.Application.Commands;
namespace ConquerServer.Infrastructure.Persistence;
namespace ConquerServer.Presentation.Network.Handlers;
namespace ConquerServer.BackgroundServices;
```

---

## 🚫 **Anti-Patterns to Avoid**

### **❌ NEVER Do This**:
```csharp
// Domain depending on infrastructure
public class Player
{
    private readonly IDbContext _context; // NO! Domain can't know about EF
}

// Business logic in packet handlers
public class MovePacketHandler
{
    public async Task Handle(MsgAction packet)
    {
        if (packet.X < 0) return; // NO! Validation belongs in Domain
    }
}

// Using HTTP/REST in a TCP game server
[ApiController] // NO! This is a console app, not web API
public class PlayerController { }
```

### **✅ Correct Patterns**:
```csharp
// Pure domain logic
public class Player
{
    public bool CanMoveTo(Position newPosition, Map map)
    {
        return map.IsValidPosition(newPosition); // Pure business logic
    }
}

// Infrastructure implements domain contracts
public class SqlPlayerRepository : IPlayerRepository
{
    private readonly ConquerDbContext _context;
    // EF implementation details here
}
```

---

## 🎯 **Configuration Structure**

### **appsettings.json**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "ConquerServer": {
    "Port": 5816,
    "MaxPlayers": 1000,
    "GameVersion": "5065",
    "MapPath": "./maps/",
    "DatabasePath": "./database/"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ConquerServer;Trusted_Connection=true"
  }
}
```

---

## 🎮 **Common Conquer Features to Implement**

### **Phase 1 - Core**:
- [ ] Player login/logout
- [ ] Basic movement
- [ ] Map loading
- [ ] Chat system
- [ ] Item system basics

### **Phase 2 - Gameplay**:
- [ ] Combat system
- [ ] Experience and leveling
- [ ] Skill system
- [ ] Trading system
- [ ] Guild system

### **Phase 3 - Advanced**:
- [ ] Monster AI
- [ ] Quest system
- [ ] PK system
- [ ] Guild wars
- [ ] Events and tournaments

---

## 💡 **Key Decision Rules**

### **"Where does this code go?"**

1. **Is it a Conquer game rule?** → **Domain**
   - Player can only attack within 3 tiles
   - Warriors get +20% physical attack
   - Items decay after 20 minutes

2. **Is it coordinating game actions?** → **Application**
   - Handle player login (authenticate, load data, notify)
   - Process item use (validate, apply effects, save)

3. **Is it technical implementation?** → **Infrastructure**
   - TCP packet encryption/decryption
   - SQL database queries
   - File-based map loading

4. **Is it handling network protocol?** → **Presentation.Network**
   - Parse MsgAction packets
   - Handle client disconnections
   - Rate limiting

5. **Is it a background process?** → **BackgroundServices**
   - Game loop ticking
   - Monster spawning
   - Player auto-save

---

## 🚀 **Quick Reference Commands**

### **Create New Feature**:
1. Start with Domain entity/service
2. Add Application command/handler
3. Implement Infrastructure repository
4. Create Network packet handler
5. Wire up in DI container

### **Example Flow: Add New Item System**:
1. `Domain/Entities/Item.cs` - Item entity with business rules
2. `Domain/Services/ItemService.cs` - Item usage logic
3. `Application/Commands/UseItemCommand.cs` - Use item command
4. `Application/Handlers/UseItemHandler.cs` - Command handler
5. `Infrastructure/Persistence/SqlItemRepository.cs` - Database implementation
6. `Presentation.Network/Handlers/ItemPacketHandler.cs` - Network handling

---

## 📚 **Remember These Principles**

1. **Console Application**: This is NOT a web API - no HTTP, no REST
2. **TCP Protocol**: All client communication via binary TCP packets
3. **Clean Architecture**: Dependencies point inward toward Domain
4. **Conquer-Specific**: All examples should be relevant to Conquer gameplay
5. **Domain-First**: Always start with business logic, then infrastructure
6. **No Shortcuts**: Follow the architecture even for simple features

---

**Use these instructions consistently to maintain architectural integrity across the entire Conquer server codebase.**