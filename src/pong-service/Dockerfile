FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as build
COPY pong-service.csproj /src/pong-service.csproj
WORKDIR /src
RUN dotnet restore
COPY . /src
RUN dotnet publish -c Release -o output

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 as runtime
COPY --from=build /src/output /app
WORKDIR /app
ENTRYPOINT [ "dotnet", "pong-service.dll","--urls", "http://*:5000" ]