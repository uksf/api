# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Test Commands

```bash
# Build
dotnet build -c Release

# Run all tests
dotnet test

# Run specific test project
dotnet test Tests/UKSF.Api.Tests/UKSF.Api.Tests.csproj

# Run tests with coverage (as used in CI)
dotnet test **/*.Tests*.csproj --configuration Release /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Run the API
dotnet run --project UKSF.Api/UKSF.Api.csproj

# Publish
dotnet publish UKSF.Api/UKSF.Api.csproj -c Release -o bin/UKSF.Api
```

## Architecture Overview

This is a .NET 10 modular monolith API for UKSF, using MongoDB for data persistence and SignalR for real-time communication.

### Solution Structure

- **UKSF.Api** - Main ASP.NET Core entry point
- **UKSF.Api.Core** - Shared infrastructure (data contexts, authentication, logging, base services)
- **UKSF.Api.Modpack** - Modpack building and Steam Workshop mod processing
- **UKSF.Api.ArmaServer** - Game server management
- **UKSF.Api.ArmaMissions** - Mission file management
- **UKSF.Api.Launcher** - Launcher functionality (incomplete/abandoned project — ignore Launcher-related issues)
- **UKSF.Api.Integrations.*** - Discord, Teamspeak, Instagram integrations
- **Tests/** - Test projects mirror source structure with `.Tests` suffix

### Dependency Injection Pattern

Each module registers services via extension methods following this convention:
- `ApiModpackExtensions.cs` → `AddModpack()`
- `ApiArmaServerExtensions.cs` → `AddArmaServer()`

These are composed in `UKSF.Api/Extensions/ServiceExtensions.cs` which calls:
- `AddContexts()` - MongoDB data access
- `AddEventHandlers()` - Domain event subscribers
- `AddServices()` - Business logic
- `AddCommands()` / `AddQueries()` - CQRS pattern
- `AddActions()` - Scheduled background tasks

### Data Access

MongoDB with custom abstraction layer:
- **MongoContext<T>** - Standard async context with CRUD operations
- **CachedMongoContext<T>** - In-memory cached version for frequently accessed data
- All domain models extend `MongoObject`
- Collections registered via DI in context registration

### Authentication

JWT token-based with multiple schemes:
- JwtBearer (primary API auth)
- Steam OpenID
- Cookie-based sessions

### Real-time Communication

SignalR hubs for live updates:
- Core: `AllHub`, `AccountHub`, `NotificationHub`
- Modules have their own hubs: `ServersHub`, `ModpackHub`

### Background Processing

- **IScheduledAction** - Recurring tasks (log pruning, server update checks)
- **MassTransit** - Saga-based state machine for workshop mod operations (`WorkshopModStateMachine`)

### Testing

- xUnit with FluentAssertions and Moq
- Mongo2Go for in-memory MongoDB during tests
- `UKSF.Api.Tests.Common` provides shared test utilities
- If builds or tests fail due to file locks from a running API process, stop the API process

### Test Safety

**CRITICAL: Tests run on a CI agent that shares the live production environment.** The test runner executes on the same box that runs live Teamspeak, Discord bots, game servers, and other services. A test that accidentally connects to a real service could disrupt production. All tests must be completely isolated.

**All external dependencies MUST be mocked.** Never write tests that interact with real services.

#### Teamspeak
- Mock `ITeamspeakService`, `ITeamspeakManagerService`, `ITeamspeakGroupService`
- Mock `IHubContext<TeamspeakHub, ITeamspeakClient>`
- Never call real process launch/shutdown (ts3server.exe, ts3client_win64.exe)
- Never send real Teamspeak procedures or messages

#### Discord
- Mock `IDiscordClientService`, `IDiscordMessageService`, `IDiscordMembersService`
- Mock `DiscordSocketClient` (Discord.Net library)
- Never call `Connect()`, `LoginAsync()`, or `StartAsync()` on real clients
- Never send real messages to Discord channels

#### HTTP Clients / External APIs
- Mock `ISteamApiService` — never call real Steam Workshop API
- Mock `IInstagramService` — never call real Instagram Graph API
- Mock `IGithubService`, `IGithubClientService` — never call real GitHub API
- Mock `IGameServersService` for server status HTTP checks
- Never make real HTTP requests to external domains

#### MongoDB
- Use **Mongo2Go** for integration tests (in-memory MongoDB)
- Mock `IMongoDatabase`, `IMongoCollection<T>` for unit tests
- Mock specific context interfaces (e.g., `IAccountContext`, `IBuildsContext`)
- **Never connect to the live MongoDB instance**

#### File System
- Mock `IFileSystemService` for file operations
- If temp files are needed, use `Path.GetTempPath()` with cleanup in teardown
- **Never read from or write to production paths** (modpack dirs, mission dirs, config files)
- Mock `IMissionService`, `IModpackService`, `ILauncherFileService` for file-heavy services

#### Process Execution (External Programs)
- Mock `IProcessUtilities` — never launch real processes (game servers, Teamspeak, etc.)
- Mock `IProcessCommandFactory` and `IProcessCommand`
- Mock `ISteamCmdService` — never run real steamcmd.exe
- Mock `IGitService` — never run real git commands that push or modify repos
- Mock `IBuildsService` process launching
- **Never call `Process.Start` against real executables** (SteamCMD, git, server binaries, Teamspeak)

#### Email / SMTP
- Mock `ISmtpClientContext`
- Mock `ISendBasicEmailCommand`, `ISendTemplatedEmailCommand`
- **Never send real emails** via SMTP

#### SignalR Hubs
- Mock `IHubContext<THub, TClient>` for all hubs
- Mock specific hub client interfaces (`IModpackClient`, `IServersClient`, etc.)
- Hub routing logic can be tested, but client-facing calls must be mocked

#### MassTransit / Message Bus
- Mock `IPublishEndpoint` and `IBus`
- Saga state machine logic can be tested with the MassTransit test harness
- Mock all side-effect consumers

#### Scheduled Actions
- Mock `IScheduledAction` implementations and `ISchedulerService`
- **Never let scheduled actions execute** — they trigger external calls (Teamspeak snapshots, Instagram token refresh, log cleanup)

#### Acceptable Real Interactions
- Reading test fixture files from TestData directories
- Executing safe, harmless process commands (echo, sleep) for testing process execution infrastructure only
- In-memory event bus and mock contexts
- Mongo2Go in-memory database

## General Instructions

- Don't patronise or affirm me as part of your responses. No "You're absolutely right!" type responses.
- Always use latest language and framework standards
- Try to match implementations in the codebase where possible. If that implementation is inferior to another, use the better one. My code is not the best and has been a playground for experimentation. Refactors are welcome as long as they are validated with complete tests.
- Aim to make code simple and clean
- Avoid excessive comments. Code should be self-explanatory through clear naming and simple structure. Do not add comments that merely restate what can be inferred from the code itself.
- Do not create summary documents unless explicitly asked to do so
- When investigating bugs and issues, you should always write a failing test first to replicate the behaviour. The expectation is that these tests should then pass when the implementation is fixed. You must always triple check the test fails for the correct reason, to be highly certain it is not failing for an unrelated reason. When taking this approach, do not continue if the tests are unable to run or you cannot get test output to verify the test does fail in practice.
- Always write tests for the whole code files that are being edited, but don't duplicate coverage. Do this before making implementation changes and verify the tests pass before editing implementation code files