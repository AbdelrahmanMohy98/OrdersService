FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/OrdersService.Api/OrdersService.Api.csproj", "src/OrdersService.Api/"]
RUN dotnet restore "src/OrdersService.Api/OrdersService.Api.csproj"

COPY . .
WORKDIR /src/src/OrdersService.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrdersService.Api.dll"]
