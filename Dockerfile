FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Directory.Build.props ./
COPY NuGet.Config ./
COPY src/ ./src/
RUN for project in Hub.Web Hub.Gateway Hub.Tools Hub.Worker; do dotnet publish src/$project/$project.csproj -c Release -o /out/$project --nologo || exit 1; done

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG PROJECT=Hub.Web
ENV HUB_ASSEMBLY=${PROJECT}.dll ASPNETCORE_HTTP_PORTS=8080
WORKDIR /app
COPY --from=build /out/${PROJECT} .
RUN mkdir -p /data/uploads /data/keys && chown -R app:app /data
USER app
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet \"$HUB_ASSEMBLY\""]
