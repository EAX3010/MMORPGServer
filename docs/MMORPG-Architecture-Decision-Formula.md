# 📋 MMORPG Server Architecture Decision Formula

> **Quick Reference Guide**: Use this formula to instantly know where any file or folder belongs in your Clean Architecture MMORPG server.

---

## 🎯 **The Universal Decision Formula**

### **Step 1: Identify the Nature**
Ask: **"What IS this code doing?"**

```
IF (pure game rule/business logic) → DOMAIN
ELSE IF (coordinates multiple actions) → APPLICATION  
ELSE IF (technical implementation) → INFRASTRUCTURE
ELSE IF (handles network protocol) → PRESENTATION.NETWORK
ELSE IF (background process) → BACKGROUNDSERVICES
ELSE IF (startup/main entry) → SERVERHOST
```

### **Step 2: Apply the Layer-Specific Rules**

---

## 🎮 **DOMAIN LAYER Rules**

### **Decision Questions:**
- ✅ "Would a game designer care about this rule?"
- ✅ "Does this exist in the game world?"
- ✅ "Is this a business rule that never changes regardless of technology?"

### **File Placement Formula:**
```
Domain/
├── Entities/           IF (main game object with identity + behavior)
├── ValueObjects/       IF (immutable concept without identity)
├── Enums/             IF (game state/type enumeration)
├── Services/          IF (complex business rule involving multiple entities)
├── Repositories/      IF (data access contract - interface only!)
├── Events/            IF (something that happens in game world)
├── Aggregates/        IF (cluster of related entities - advanced)
├── Interfaces/        IF (domain contract that's not repository)
└── Exceptions/        IF (business rule violation)
```

### **Examples:**
```csharp
// ✅ Domain/Entities/Player.cs
public class Player { uint Id; string Name; void LevelUp(); }

// ✅ Domain/ValueObjects/Position.cs  
public record Position(float X, float Y);

// ✅ Domain/Services/CombatService.cs
public class CombatService { bool CanAttack(Player attacker, Player target); }

// ✅ Domain/Events/PlayerLevelUpEvent.cs
public record PlayerLevelUpEvent(uint PlayerId, uint NewLevel);
```

---

## 🎭 **APPLICATION LAYER Rules**

### **Decision Questions:**
- ✅ "Does this coordinate multiple domain services?"
- ✅ "Is this a use case/user story?"
- ✅ "Does this orchestrate without containing business logic?"

### **File Placement Formula:**
```
Application/
├── Commands/          IF (player wants to DO something)
│   └── {Feature}/     Group by game feature (Player/, Item/, Combat/)
├── Queries/           IF (player wants to GET information)
│   └── {Feature}/     Group by game feature
├── Handlers/          IF (processes commands/queries)
│   ├── Commands/      Handle actions
│   └── Queries/       Handle data requests
├── Services/          IF (orchestrates multiple domain services)
├── Models/            IF (data transfer object for this layer)
├── Common/            IF (shared application utilities)
├── Features/          IF (using feature-based organization)
└── Extensions/        IF (dependency injection registration)
```

### **Examples:**
```csharp
// ✅ Application/Commands/Player/MovePlayerCommand.cs
public record MovePlayerCommand(uint PlayerId, float X, float Y);

// ✅ Application/Handlers/Commands/MovePlayerHandler.cs
public class MovePlayerHandler { /* orchestrates movement */ }

// ✅ Application/Services/GameSessionService.cs
public class GameSessionService { /* coordinates login flow */ }
```

---

## 🔧 **INFRASTRUCTURE LAYER Rules**

### **Decision Questions:**
- ✅ "Does this implement a domain interface?"
- ✅ "Is this technology/framework specific?"
- ✅ "Would this change if we swap databases/protocols?"

### **File Placement Formula:**
```
Infrastructure/
├── Persistence/       IF (data storage implementation)
│   ├── Contexts/      EF DbContext classes
│   ├── Repositories/  Domain repository implementations
│   ├── Configurations/ EF entity configurations
│   ├── Migrations/    Database schema changes
│   └── Seeders/       Initial data loading
├── Networking/        IF (TCP/network implementation)
│   ├── Server/        TCP server components
│   ├── Clients/       Client connection management
│   ├── Protocols/     Binary protocol handling
│   └── Handlers/      Low-level packet processing
├── Security/          IF (encryption/auth implementation)
├── Services/          IF (infrastructure service implementations)
├── External/          IF (third-party service integrations)
└── Extensions/        IF (DI registration for this layer)
```

### **Examples:**
```csharp
// ✅ Infrastructure/Persistence/Repositories/SqlPlayerRepository.cs
public class SqlPlayerRepository : IPlayerRepository { /* SQL implementation */ }

// ✅ Infrastructure/Networking/Server/TcpGameServer.cs
public class TcpGameServer { /* TCP server implementation */ }
```

---

## 🖥️ **PRESENTATION.NETWORK LAYER Rules**

### **Decision Questions:**
- ✅ "Does this handle game protocol packets?"
- ✅ "Does this convert external input to internal commands?"
- ✅ "Is this client-facing network communication?"

### **File Placement Formula:**
```
Presentation.Network/
├── Handlers/          IF (processes specific packet types)
│   ├── Authentication/ Login/logout packets
│   ├── Movement/      Movement packets
│   ├── Combat/        Attack/skill packets
│   ├── Items/         Item operation packets
│   ├── Social/        Chat/guild packets
│   └── System/        Ping/info packets
├── Protocols/         IF (packet structure definitions)
│   ├── Packets/       Incoming packet classes
│   └── Responses/     Outgoing packet classes
├── Services/          IF (network-layer services)
├── Middleware/        IF (packet processing pipeline)
└── Extensions/        IF (DI registration)
```

### **Examples:**
```csharp
// ✅ Presentation.Network/Handlers/Movement/MoveHandler.cs
public class MoveHandler { /* converts MovePacket to MovePlayerCommand */ }

// ✅ Presentation.Network/Protocols/Packets/LoginPacket.cs
public class LoginPacket { /* packet structure */ }
```

---

## 🔄 **BACKGROUNDSERVICES LAYER Rules**

### **Decision Questions:**
- ✅ "Does this run continuously in the background?"
- ✅ "Is this a scheduled/periodic task?"
- ✅ "Does this process game world updates?"

### **File Placement Formula:**
```
BackgroundServices/
├── Core/              IF (essential game services)
│   ├── GameLoopService.cs      Main game tick
│   ├── NetworkListenerService.cs TCP listener
│   └── GameTickService.cs      Tick coordinator
├── Game/              IF (game mechanic services)
│   ├── MonsterAIService.cs     NPC behavior
│   ├── RespawnService.cs       Entity respawning
│   └── CombatProcessorService.cs Combat resolution
├── Maintenance/       IF (maintenance/cleanup tasks)
├── Monitoring/        IF (metrics/health monitoring)
├── Events/            IF (event processing services)
├── Abstractions/      IF (background service interfaces)
└── Extensions/        IF (DI registration)
```

---

## 🚀 **SERVERHOST LAYER Rules**

### **Decision Questions:**
- ✅ "Is this the main entry point?"
- ✅ "Does this configure dependency injection?"
- ✅ "Is this startup/shutdown logic?"

### **File Placement Formula:**
```
ServerHost/
├── Program.cs         Main entry point
├── appsettings.json   Configuration files
├── Configuration/     IF (DI setup/configuration classes)
├── Commands/          IF (console command implementations)
└── Utilities/         IF (host-specific utilities)
```

---

## 📐 **Quick Reference Decision Tree**

```
New File/Class → Ask Questions → Place in Layer

1. "Is this a game rule/entity?"
   YES → Domain/{appropriate folder}
   NO → Continue

2. "Does this coordinate multiple actions?"
   YES → Application/{Commands|Queries|Handlers|Services}
   NO → Continue

3. "Is this a technical implementation?"
   YES → Infrastructure/{Persistence|Networking|Security}
   NO → Continue

4. "Does this handle network packets?"
   YES → Presentation.Network/Handlers/{feature}
   NO → Continue

5. "Does this run in background?"
   YES → BackgroundServices/{Core|Game|Maintenance}
   NO → Continue

6. "Is this startup/main entry?"
   YES → ServerHost/
```

---

## 🎯 **Pattern Examples by Feature**

### **Adding Player Movement Feature:**
1. `Domain/Entities/Player.cs` → `MoveTo(Position pos)` method
2. `Domain/Services/MovementService.cs` → Movement validation rules
3. `Application/Commands/Player/MovePlayerCommand.cs` → Command definition
4. `Application/Handlers/Commands/MovePlayerHandler.cs` → Orchestration
5. `Infrastructure/Persistence/Repositories/SqlPlayerRepository.cs` → Save position
6. `Presentation.Network/Handlers/Movement/MoveHandler.cs` → Packet processing

### **Adding Item System Feature:**
1. `Domain/Entities/Item.cs` → Item entity
2. `Domain/Services/ItemService.cs` → Item usage rules
3. `Application/Commands/Item/UseItemCommand.cs` → Use item command
4. `Application/Handlers/Commands/UseItemHandler.cs` → Item use orchestration
5. `Infrastructure/Persistence/Repositories/SqlItemRepository.cs` → Item storage
6. `Presentation.Network/Handlers/Items/ItemHandler.cs` → Item packets

### **Adding Combat System Feature:**
1. `Domain/Services/CombatService.cs` → Combat calculation rules
2. `Domain/Events/PlayerDeathEvent.cs` → Combat events
3. `Application/Commands/Combat/AttackCommand.cs` → Attack command
4. `Application/Handlers/Commands/AttackHandler.cs` → Combat orchestration
5. `BackgroundServices/Game/CombatProcessorService.cs` → Combat resolution
6. `Presentation.Network/Handlers/Combat/AttackHandler.cs` → Attack packets

---

## 💡 **Memory Aids**

- **Domain** = "Game Designer's World" (pure rules)
- **Application** = "Use Case Coordinator" (orchestrates)
- **Infrastructure** = "Technology Implementation" (concrete)
- **Presentation.Network** = "Protocol Translator" (packets ↔ commands)
- **BackgroundServices** = "Always Running" (background tasks)
- **ServerHost** = "Main Entry" (startup/DI)

---

## 🚫 **What NOT to Put Where**

### **❌ NEVER put in Domain:**
- Database queries (EF DbContext)
- Network sockets (TcpClient)
- File I/O operations
- HTTP calls
- Framework dependencies

### **❌ NEVER put in Application:**
- Business logic calculations
- Database implementation details
- Network protocol specifics
- UI/packet formatting

### **❌ NEVER put in Infrastructure:**
- Business rules
- Use case orchestration
- Domain events
- Application services

---

## 🎮 **Final Rule**

**When in doubt, ask: "If I changed from TCP to UDP, from SQL Server to MongoDB, from Console to Web - would this code change?"**

- **YES** → Infrastructure
- **NO** → Domain or Application

**Use this formula every time and you'll never place a file in the wrong location!**