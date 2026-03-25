# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish SmartVestFinancialAdvisor/SmartVestFinancialAdvisor.csproj -c Release -o /app/publish

# --- Run stage ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
# Render provides $PORT; ASP.NET honors ASPNETCORE_URLS
ENV ASPNETCORE_URLS=http://0.0.0.0:$PORT
COPY --from=build /app/publish ./
# If your output
