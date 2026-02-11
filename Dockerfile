# استخدام صورة .NET الرسمية
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# نسخ ملف المشروع واستعادته
COPY *.csproj ./
RUN dotnet restore

# نسخ باقي الملفات وبناء التطبيق
COPY . ./
RUN dotnet publish -c Release -o out

# صورة التشغيل النهائية
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# تعريض البورت 80
EXPOSE 8080
ENTRYPOINT ["dotnet", "GoldTrackerWeb.dll"]