FROM mcr.microsoft.com/dotnet/aspnet:3.1 AS base
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgdiplus libc6-dev \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /src
COPY ["HPorvenir.Web.Api/HPorvenir.Web.Api.csproj", "HPorvenir.Web.Api/"]
COPY ["HPorvenir.Navegation/HPorvenir.Navegation.csproj", "HPorvenir.Navegation/"]
RUN dotnet restore "HPorvenir.Web.Api/HPorvenir.Web.Api.csproj"
COPY . .
WORKDIR "/src/HPorvenir.Web.Api"
RUN dotnet build "HPorvenir.Web.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HPorvenir.Web.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HPorvenir.Web.Api.dll"]