VERSION="0.3.0"
PACKAGE_NAME=Coflnet.Sky.Settings.Client

docker run --rm -v "${PWD}:/local" --network host -u $(id -u ${USER}):$(id -g ${USER})  openapitools/openapi-generator-cli generate \
-i http://localhost:5004/api/openapi/v1/openapi.json \
-g csharp \
-o /local/out --additional-properties=packageName=$PACKAGE_NAME,packageVersion=$VERSION,licenseId=MIT,targetFramework=net10.0,library=restsharp

cd out
sed -i 's/GIT_USER_ID/Coflnet/g' src/$PACKAGE_NAME/$PACKAGE_NAME.csproj
sed -i 's/GIT_REPO_ID/SkySettings/g' src/$PACKAGE_NAME/$PACKAGE_NAME.csproj
sed -i 's/>OpenAPI/>Coflnet/g' src/$PACKAGE_NAME/$PACKAGE_NAME.csproj

sed -i 's@annotations</Nullable>@annotations</Nullable>\n    <PackageReadmeFile>README.md</PackageReadmeFile>@g' $path
sed -i '34i    <None Include="../../../../README.md" Pack="true" PackagePath="\"/>' $path

dotnet pack
cp src/$PACKAGE_NAME/bin/Release/$PACKAGE_NAME.*.nupkg ..
dotnet nuget push ../$PACKAGE_NAME.$VERSION.nupkg --api-key $NUGET_API_KEY --source "nuget.org" --skip-duplicate