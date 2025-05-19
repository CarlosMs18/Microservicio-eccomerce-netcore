FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 5001
ENV ASPNETCORE_ENVIRONMENT=Docker
ENV DOTNET_RUNNING_IN_CONTAINER=true

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["src/User/User.WebAPI/User.WebAPI.csproj", "src/User/User.WebAPI/"]
RUN dotnet restore "src/User/User.WebAPI/User.WebAPI.csproj"
COPY . .
WORKDIR "/src/src/User/User.WebAPI"
RUN dotnet build "User.WebAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "User.WebAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "User.WebAPI.dll"]