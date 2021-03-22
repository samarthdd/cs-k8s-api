#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["Source/Common/Glasswall.CloudProxy.Common/Glasswall.CloudProxy.Common.csproj", "Source/Common/Glasswall.CloudProxy.Common/"]
COPY ["Source/Service/Glasswall.CloudProxy.Api.csproj", "Source/Service/"]
RUN dotnet restore "Source/Service/Glasswall.CloudProxy.Api.csproj"
COPY . .
WORKDIR "/src/Source/Service"
RUN dotnet build "Glasswall.CloudProxy.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Glasswall.CloudProxy.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Glasswall.CloudProxy.Api.dll"]