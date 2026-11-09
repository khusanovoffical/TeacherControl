FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5123
ENV ASPNETCORE_URLS=http://+:5123

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["SchoolControl.Server.csproj", "."]
RUN dotnet restore "./SchoolControl.Server.csproj"
COPY . .
RUN dotnet build "SchoolControl.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SchoolControl.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SchoolControl.Server.dll"]
