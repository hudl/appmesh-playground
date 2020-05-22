FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as build
COPY ping-service.csproj /src/ping-service.csproj
WORKDIR /src
RUN dotnet restore
COPY . /src
RUN dotnet publish -c Release -o output

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 as runtime
COPY --from=build /src/output /app
WORKDIR /app
ENTRYPOINT [ "dotnet", "ping-service.dll","--urls", "http://*:5000" ]