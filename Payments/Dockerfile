FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1. Копируем только файл проекта Payments
COPY ["Payments/Payments.csproj", "Payments/"]
RUN dotnet restore "Payments/Payments.csproj"

# 2. Копируем ТОЛЬКО содержимое папки Payments
COPY Payments/ Payments/

# 3. Устанавливаем рабочую директорию в папку проекта
WORKDIR "/src/Payments"
RUN dotnet build "Payments.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Payments.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Payments.dll"]