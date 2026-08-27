# 编译阶段
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Keisoft.IM.Http/Keisoft.IM.Http.csproj", "Keisoft.IM.Http/"]

# 还原 NuGet 包
RUN dotnet restore "./Keisoft.IM.Http/Keisoft.IM.Http.csproj"

# 拷贝全部源码
COPY . .
WORKDIR "/src/Keisoft.IM.Http"
RUN dotnet build "./Keisoft.IM.Http.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 发布
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Keisoft.IM.Http.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 运行时阶段：用精简的 aspnet 镜像
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Keisoft.IM.Http.dll"]