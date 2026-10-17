FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/TaskFlow.Api/ src/TaskFlow.Api/
RUN dotnet publish src/TaskFlow.Api/TaskFlow.Api.csproj -c Release -o /app --self-contained false
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TaskFlow.Api.dll"]
