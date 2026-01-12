FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

COPY ["services/payment-service/PaymentService.csproj", "services/payment-service/"]
COPY ["building-blocks/BuildingBlocks/BuildingBlocks.csproj", "building-blocks/BuildingBlocks/"]

RUN dotnet restore "services/payment-service/PaymentService.csproj"

COPY . .

RUN dotnet build "services/payment-service/PaymentService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "services/payment-service/PaymentService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=publish /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "PaymentService.dll"]
