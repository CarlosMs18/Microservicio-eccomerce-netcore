FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 5003 
ENV ASPNETCORE_ENVIRONMENT=Docker
ENV DOTNET_RUNNING_IN_CONTAINER=true

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["src/Catalog/Catalog.WebApi/Catalog.WebAPI.csproj", "src/Catalog/Catalog.WebApi/"]
RUN dotnet restore "src/Catalog/Catalog.WebApi/Catalog.WebAPI.csproj"
COPY . .
WORKDIR "/src/src/Catalog/Catalog.WebApi"
RUN dotnet build "Catalog.WebAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Catalog.WebAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Catalog.WebAPI.dll"]