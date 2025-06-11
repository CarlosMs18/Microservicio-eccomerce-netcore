FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 5004
ENV ASPNETCORE_ENVIRONMENT=Docker
ENV DOTNET_RUNNING_IN_CONTAINER=true

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["src/Cart/Cart.WebAPI/Cart.WebAPI.csproj", "src/Cart/Cart.WebAPI/"]
RUN dotnet restore "src/Cart/Cart.WebAPI/Cart.WebAPI.csproj"
COPY . .
WORKDIR "/src/src/Cart/Cart.WebAPI"
RUN dotnet build "Cart.WebAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Cart.WebAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Cart.WebAPI.dll"]