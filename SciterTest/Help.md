Работает только не собранная версия потому что файлы в собранном виде не найдутся.

для починки сраного CEO отрубаем .NET 10 принудительнофайлом global.json возле сборки (.slnx) с контекстом
```
{
  "sdk": {
    "version": "8.0.100",
    "rollForward": "latestFeature"
  }
}
```

dotnet publish -c Release

dotnet publish -c Release -r win-x64 --self-contained false


dotnet publish -c Release -r linux-x64 --self-contained
