# Render has no native .NET runtime, so the API ships as a container.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The .csproj is copied and restored ON ITS OWN, before the rest of the source.
# Docker caches each instruction as a layer and reuses it while its inputs are unchanged. Copying
# only the project file first means the expensive restore is invalidated by a change to the
# DEPENDENCY LIST, not by editing a controller - so ordinary code changes skip re-downloading
# every NuGet package. Copying everything up front would throw that cache away on every commit.
COPY ["Takeaway-OS.API/Takeaway-OS.API.csproj", "Takeaway-OS.API/"]
RUN dotnet restore "Takeaway-OS.API/Takeaway-OS.API.csproj"

COPY Takeaway-OS.API/ Takeaway-OS.API/
RUN dotnet publish "Takeaway-OS.API/Takeaway-OS.API.csproj" -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Run as the unprivileged user Microsoft's .NET images ship with, instead of root.
# A container's root is close enough to the host's root that a compromised app running as it has
# far more room to do damage (write anywhere in the image, install tools). The API needs none of
# that: it only reads its own files and opens a network port.
# $APP_UID is an environment variable the base image defines for exactly this line (it holds the
# user's numeric id), so nothing here hard-codes a user name or number.
# Works with the port below because 8080 is above 1024: ports under 1024 need root to listen on.
USER $APP_UID

# Render assigns a port at runtime through $PORT and routes traffic to it; a container listening
# anywhere else is treated as a failed deploy. ASP.NET takes its address from ASPNETCORE_URLS.
#
# That variable cannot be set with ENV, because ENV is evaluated when the IMAGE IS BUILT and $PORT
# does not exist until the container runs. The shell form of CMD runs through /bin/sh at start-up,
# which is what expands it. 8080 is the fallback so the same image runs locally with no $PORT set.
#
# 0.0.0.0 rather than localhost: inside a container, localhost means the container itself, and the
# port would not be reachable from outside it.
EXPOSE 8080
CMD ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet Takeaway-OS.API.dll
