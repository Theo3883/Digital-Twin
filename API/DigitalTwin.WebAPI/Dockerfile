FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY DigitalTwin.Domain/DigitalTwin.Domain.csproj DigitalTwin.Domain/
COPY DigitalTwin.Application/DigitalTwin.Application.csproj DigitalTwin.Application/
COPY DigitalTwin.Infrastructure/DigitalTwin.Infrastructure.csproj DigitalTwin.Infrastructure/
COPY DigitalTwin.Composition/DigitalTwin.Composition.csproj DigitalTwin.Composition/
COPY DigitalTwin.WebAPI/DigitalTwin.WebAPI.csproj DigitalTwin.WebAPI/
RUN dotnet restore DigitalTwin.WebAPI/DigitalTwin.WebAPI.csproj

COPY DigitalTwin.Domain/ DigitalTwin.Domain/
COPY DigitalTwin.Application/ DigitalTwin.Application/
COPY DigitalTwin.Infrastructure/ DigitalTwin.Infrastructure/
COPY DigitalTwin.Composition/ DigitalTwin.Composition/
COPY DigitalTwin.WebAPI/ DigitalTwin.WebAPI/

RUN dotnet publish DigitalTwin.WebAPI/DigitalTwin.WebAPI.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "DigitalTwin.WebAPI.dll"]
