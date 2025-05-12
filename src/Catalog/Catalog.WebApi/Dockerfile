#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["src/Catalog/Catalog.WebApi/Catalog.WebAPI.csproj", "src/Catalog/Catalog.WebApi/"]
COPY ["src/Catalog/Catalog.Application/Catalog.Application.csproj", "src/Catalog/Catalog.Application/"]
COPY ["Shared/Shared.Core/Shared.Core.csproj", "Shared/Shared.Core/"]
COPY ["src/Catalog/Catalog.Domain/Catalog.Domain.csproj", "src/Catalog/Catalog.Domain/"]
COPY ["src/Catalog/Catalog.Infrastructure/Catalog.Infrastructure.csproj", "src/Catalog/Catalog.Infrastructure/"]
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