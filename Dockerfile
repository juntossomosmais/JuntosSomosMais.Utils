FROM mcr.microsoft.com/dotnet/sdk:8.0

WORKDIR /app

# Restores (downloads) all NuGet packages on a separate layer for caching
COPY *.sln ./
COPY src/Directory.Build.props src/
COPY tests/Directory.Build.props tests/
COPY src/JuntosSomosMais.Utils.GlobalExceptionHandler/*.csproj src/JuntosSomosMais.Utils.GlobalExceptionHandler/packages.lock.json src/JuntosSomosMais.Utils.GlobalExceptionHandler/
COPY tests/JuntosSomosMais.Utils.GlobalExceptionHandler.Tests/*.csproj tests/JuntosSomosMais.Utils.GlobalExceptionHandler.Tests/packages.lock.json tests/JuntosSomosMais.Utils.GlobalExceptionHandler.Tests/
RUN dotnet restore --locked-mode

# Tools used during development
COPY dotnet-tools.json ./
RUN dotnet tool restore

COPY . ./
