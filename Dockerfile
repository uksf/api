FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS publish
WORKDIR /src
COPY ["UKSF.Api/UKSF.Api.csproj", "UKSF.Api/"]
COPY ["UKSF.Api.Admin/UKSF.Api.Admin.csproj", "UKSF.Api.Admin/"]
COPY ["UKSF.Api.Base/UKSF.Api.Base.csproj", "UKSF.Api.Base/"]
COPY ["UKSF.Api.Shared/UKSF.Api.Shared.csproj", "UKSF.Api.Shared/"]
COPY ["UKSF.Api.Auth/UKSF.Api.Auth.csproj", "UKSF.Api.Auth/"]
COPY ["UKSF.Api.Personnel/UKSF.Api.Personnel.csproj", "UKSF.Api.Personnel/"]
COPY ["UKSF.Api.Command/UKSF.Api.Command.csproj", "UKSF.Api.Command/"]
COPY ["UKSF.Api.Integrations.Instagram/UKSF.Api.Integrations.Instagram.csproj", "UKSF.Api.Integrations.Instagram/"]
COPY ["UKSF.Api.Integrations.Discord/UKSF.Api.Integrations.Discord.csproj", "UKSF.Api.Integrations.Discord/"]
COPY ["UKSF.Api.Integrations.Teamspeak/UKSF.Api.Integrations.Teamspeak.csproj", "UKSF.Api.Integrations.Teamspeak/"]
COPY ["UKSF.Api.Launcher/UKSF.Api.Launcher.csproj", "UKSF.Api.Launcher/"]
COPY ["UKSF.Api.Modpack/UKSF.Api.Modpack.csproj", "UKSF.Api.Modpack/"]
COPY ["UKSF.Api.ArmaServer/UKSF.Api.ArmaServer.csproj", "UKSF.Api.ArmaServer/"]
COPY ["UKSF.Api.ArmaMissions/UKSF.Api.ArmaMissions.csproj", "UKSF.Api.ArmaMissions/"]
RUN dotnet restore "UKSF.Api/UKSF.Api.csproj"
COPY . .
WORKDIR "/src/UKSF.Api"
RUN dotnet publish "UKSF.Api.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UKSF.Api.dll"]
