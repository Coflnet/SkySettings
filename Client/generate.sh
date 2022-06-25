VERSION="0.1.0"

docker run --rm -v "${PWD}:/local" --network host -u $(id -u ${USER}):$(id -g ${USER})  openapitools/openapi-generator-cli generate \
-i http://localhost:5004/swagger/v1/swagger.json \
-g csharp-netcore \
-o /local/out --additional-properties=packageName=Coflnet.Sky.Settings.Client,packageVersion=$VERSION,licenseId=MIT

cd out
sed -i 's/GIT_USER_ID/Coflnet/g' src/Coflnet.Sky.Settings.Client/Coflnet.Sky.Settings.Client.csproj
sed -i 's/GIT_REPO_ID/SkySettings/g' src/Coflnet.Sky.Settings.Client/Coflnet.Sky.Settings.Client.csproj
sed -i 's/>OpenAPI/>Coflnet/g' src/Coflnet.Sky.Settings.Client/Coflnet.Sky.Settings.Client.csproj

dotnet pack
cp src/Coflnet.Sky.Settings.Client/bin/Debug/Coflnet.Sky.Settings.Client.*.nupkg ..
