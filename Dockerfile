# REDHAT Docker file
FROM {dockerRegistry}/dotnet/dotnet-31-rhel7 AS build-env
WORKDIR /app

# switch to root
USER 0
RUN mkdir obj

# Copy csproj and restore as distinct layers
COPY . ./

# REDHAT specific - we need to enable dotnet CLI
RUN scl enable rh-dotnet31 -- dotnet restore

# run unit tets - build will fail if tests fail
# RUN scl enable rh-dotnet31 -- dotnet test Tests

# build
RUN scl enable rh-dotnet31 -- dotnet publish -c Release -o /app/out

# Build runtime image
FROM {dockerRegistry}/dotnet/dotnet-31-runtime-rhel7

WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["sh","-c","scl enable rh-dotnet31 -- dotnet AzureDevOps.PullRequestCheckService.dll"]
