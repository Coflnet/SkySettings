#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

# Single source of truth for the published client version. Bump this (and merge to main) to
# cut a release: the Package GitHub Actions workflow triggers on changes to this file.
VERSION="0.3.0"
PACKAGE_NAME=Coflnet.Sky.Settings.Client

# Where to read the OpenAPI document from. Defaults to a locally running API (the historical
# workflow: start the app with `dotnet run`, then run this script). Can also be a file path,
# e.g. one produced without starting any dependent services (used by CI).
SWAGGER_INPUT="${SWAGGER_INPUT:-http://localhost:5004/api/openapi/v1/openapi.json}"
# Generator image pinned by tag AND digest so output is reproducible; override only for testing.
# v7.23.0 is the release whose output matches the published package (same library, dependencies and
# public API incl. the DI/Configuration surface); newer generators change those and break consumers.
GENERATOR_IMAGE="${GENERATOR_IMAGE:-openapitools/openapi-generator-cli:v7.23.0@sha256:5ffccd3b0d4ac57eac443e1c9b3e2f2bb7f0a21ffe6c6701f3690d7edc78bf2d}"

docker_mounts=(-v "${PWD}:/local")
if [[ "$SWAGGER_INPUT" =~ ^https?:// ]]; then
  generator_input="$SWAGGER_INPUT"
else
  # Local file: bind-mount it at a fixed in-container path so its location on the host (which
  # may be outside $PWD, e.g. a file produced elsewhere in a CI job) doesn't matter.
  swagger_abs="$(cd "$(dirname "$SWAGGER_INPUT")" && pwd)/$(basename "$SWAGGER_INPUT")"
  docker_mounts+=(-v "${swagger_abs}:/swagger-input.json:ro")
  generator_input="/swagger-input.json"
fi

docker run --rm "${docker_mounts[@]}" --network host -u "$(id -u):$(id -g)" "$GENERATOR_IMAGE" generate \
-i "$generator_input" \
-g csharp \
-o /local/out --additional-properties=packageName=$PACKAGE_NAME,packageVersion=$VERSION,licenseId=MIT,targetFramework=net10.0,library=restsharp

cd out
path=src/$PACKAGE_NAME/$PACKAGE_NAME.csproj
sed -i 's/GIT_USER_ID/Coflnet/g' $path
sed -i 's/GIT_REPO_ID/SkySettings/g' $path
sed -i 's/>OpenAPI/>Coflnet/g' $path
sed -i 's@annotations</Nullable>@annotations</Nullable>\n    <PackageReadmeFile>README.md</PackageReadmeFile>\n    <PublishRepositoryUrl>true</PublishRepositoryUrl>@g' $path
# Force the exact repository URL a verifier reads from the .nuspec (RepositoryType is already
# "git" via the generator's own GIT_USER_ID/GIT_REPO_ID template above).
sed -i 's@<RepositoryUrl>.*</RepositoryUrl>@<RepositoryUrl>https://github.com/Coflnet/SkySettings</RepositoryUrl>@' $path
# Pack the README. Anchored on the (always-present, unique) closing </PropertyGroup> tag instead
# of a line number: the generator rewrites this file from scratch on every run, so a line number
# drifts the moment the template gains or loses a line, silently dropping the README from the package.
sed -i '/<\/PropertyGroup>/a\
\
  <ItemGroup>\
    <None Include="..\/..\/..\/..\/README.md" Pack="true" PackagePath="\\"\/>\
  </ItemGroup>' $path

pack_args=()
if [[ -n "${GITHUB_SHA:-}" ]]; then
  # Embeds <repository type="git" url="..." commit="$GITHUB_SHA"/> in the .nuspec so a verifier
  # can tie the published package back to the exact commit it was built from.
  pack_args+=(-p:RepositoryCommit="$GITHUB_SHA")
fi
dotnet pack "${pack_args[@]}"
cp src/$PACKAGE_NAME/bin/Release/$PACKAGE_NAME.*.nupkg ..
rm -f *.sln

# This script only packs. Releases are pushed by the Package workflow
# (.github/workflows/package.yml) using NuGet trusted publishing; no API key is used here.
echo "Packed $PACKAGE_NAME.$VERSION into $PWD/.. only; the Package workflow publishes it on merge to main."
