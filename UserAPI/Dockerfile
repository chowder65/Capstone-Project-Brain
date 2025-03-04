FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution and project files from the root context
COPY *.sln .
COPY *.csproj ./
RUN dotnet restore

# Copy the rest of the files
COPY . .
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./
EXPOSE 5168
ENTRYPOINT ["dotnet", "UserAPI.dll"]