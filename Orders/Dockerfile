FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1. Копируем только файл проекта Orders
COPY ["Orders/Orders.csproj", "Orders/"]
RUN dotnet restore "Orders/Orders.csproj"

# 2. Копируем ТОЛЬКО содержимое папки Orders
COPY Orders/ Orders/

# 3. Устанавливаем рабочую директорию в папку проекта
WORKDIR "/src/Orders"
RUN dotnet build "Orders.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Orders.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Orders.dll"]