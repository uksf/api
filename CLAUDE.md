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
- **UKSF.Api.Launcher** - Launcher functionality
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

## General Instructions

- Don't patronise or affirm me as part of your responses. No "You're absolutely right!" type responses.
- Always use latest language and framework standards
- Try to match implementations in the codebase where possible. If that implementation is inferior to another, use the better one. My code is not the best and has been a playground for experimentation. Refactors are welcome as long as they are validated with complete tests.
- Aim to make code simple and clean
- Avoid excessive comments. Code should be self-explanatory through clear naming and simple structure. Do not add comments that merely restate what can be inferred from the code itself.
- Do not create summary documents unless explicitly asked to do so
- When investigating bugs and issues, you should always write a failing test first to replicate the behaviour. The expectation is that these tests should then pass when the implementation is fixed. You must always triple check the test fails for the correct reason, to be highly certain it is not failing for an unrelated reason. When taking this approach, do not continue if the tests are unable to run or you cannot get test output to verify the test does fail in practice.
- Always write tests for the whole code files that are being edited, but don't duplicate coverage. Do this before making implementation changes and verify the tests pass before editing implementation code files