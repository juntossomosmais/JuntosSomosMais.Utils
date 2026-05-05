FROM mcr.microsoft.com/dotnet/sdk:8.0

WORKDIR /app

# Restores (downloads) all NuGet packages on a separate layer for caching
COPY *.sln ./
COPY src/Directory.Build.props src/
COPY tests/Directory.Build.props tests/
COPY src/JuntosSomosMais.Utils.GlobalExceptionHandler/*.csproj src/JuntosSomosMais.Utils.GlobalExceptionHandler/packages.lock.json src/JuntosSomosMais.Utils.GlobalExceptionHandler/
COPY tests/JuntosSomosMais.Utils.GlobalExceptionHandler.Tests/*.csproj tests/JuntosSomosMais.Utils.GlobalExceptionHandler.Tests/packages.lock.json tests/JuntosSomosMais.Utils.GlobalExceptionHandler.Tests/
COPY src/JuntosSomosMais.Utils.Instrumentation/*.csproj src/JuntosSomosMais.Utils.Instrumentation/packages.lock.json src/JuntosSomosMais.Utils.Instrumentation/
COPY tests/JuntosSomosMais.Utils.Instrumentation.Tests/*.csproj tests/JuntosSomosMais.Utils.Instrumentation.Tests/packages.lock.json tests/JuntosSomosMais.Utils.Instrumentation.Tests/
COPY src/JuntosSomosMais.Utils.HealthChecks/*.csproj src/JuntosSomosMais.Utils.HealthChecks/packages.lock.json src/JuntosSomosMais.Utils.HealthChecks/
COPY tests/JuntosSomosMais.Utils.HealthChecks.Tests/*.csproj tests/JuntosSomosMais.Utils.HealthChecks.Tests/packages.lock.json tests/JuntosSomosMais.Utils.HealthChecks.Tests/
COPY src/JuntosSomosMais.Utils.Cnpj/*.csproj src/JuntosSomosMais.Utils.Cnpj/packages.lock.json src/JuntosSomosMais.Utils.Cnpj/
COPY tests/JuntosSomosMais.Utils.Cnpj.Tests/*.csproj tests/JuntosSomosMais.Utils.Cnpj.Tests/packages.lock.json tests/JuntosSomosMais.Utils.Cnpj.Tests/
RUN dotnet restore --locked-mode

# Tools used during development
COPY dotnet-tools.json ./
RUN dotnet tool restore

COPY . ./
