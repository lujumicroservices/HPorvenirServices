#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:3.1 AS base
RUN ["apt-get", "update"]
RUN ["apt-get", "-y", "install", "libgdiplus"]
RUN ["apt-get", "-y", "install", "xvfb", "libfontconfig", "wkhtmltopdf"]
RUN ["apt-get", "-y", "install", "libc6-dev"]
RUN ["apt-get", "-y", "install", "openssl"]
RUN ["apt-get", "-y", "install", "libssl1.0-dev"]
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