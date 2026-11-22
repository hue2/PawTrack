FROM node:22-alpine AS client
WORKDIR /src/ClientApp
COPY ClientApp/package.json ClientApp/package-lock.json ./
RUN npm ci
COPY ClientApp/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY PawTrack.Web.csproj .
RUN dotnet restore
COPY . .
COPY --from=client /src/wwwroot ./wwwroot
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PawTrack.Web.dll"]
