#!/usr/bin/env bash
# Genera regexutor-cli_<version>_amd64.deb (CLI POSIX; requiere grep del sistema).
# Ejecutar en Linux, WSL2 o contenedor (ver Dockerfile en este directorio).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
VERSION="${VERSION:-1.0.0}"
OUT="${OUT:-$ROOT/publish}"
RID="${RID:-linux-x64}"

mkdir -p "$OUT"
STAGE="$(mktemp -d)"
cleanup() { rm -rf "$STAGE"; }
trap cleanup EXIT

PKG="$STAGE/regexutor-cli_${VERSION}_amd64"
mkdir -p "$PKG/DEBIAN" "$PKG/usr/bin"

dotnet publish "$ROOT/Regexutor.Cli/Regexutor.Cli.csproj" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:Version="$VERSION" \
  -o "$STAGE/publish"

if [[ ! -f "$STAGE/publish/regexutor-cli" ]]; then
  echo "No se encontró el binario publicado: $STAGE/publish/regexutor-cli" >&2
  exit 1
fi

cp "$STAGE/publish/regexutor-cli" "$PKG/usr/bin/regexutor-cli"
chmod 0755 "$PKG/usr/bin/regexutor-cli"

INSTALLED_BYTES="$(stat -c%s "$PKG/usr/bin/regexutor-cli" 2>/dev/null || stat -f%z "$PKG/usr/bin/regexutor-cli")"

cat >"$PKG/DEBIAN/control" <<EOF
Package: regexutor-cli
Version: $VERSION
Section: utils
Priority: optional
Architecture: amd64
Maintainer: Regexutor <https://github.com/kraso/Regexutor>
Depends: grep, libc6
Description: Regexutor — línea de comandos para practicar regex POSIX BRE/ERE
 La interfaz gráfica (WPF) solo está en Windows; este paquete instala regexutor-cli,
 que usa el mismo motor que la app (grep). Requiere GNU grep en el sistema.
Installed-Size: $(( (INSTALLED_BYTES + 1023) / 1024 ))
EOF

DEB_PATH="$OUT/regexutor-cli_${VERSION}_amd64.deb"
dpkg-deb --root-owner-group --build "$PKG" "$DEB_PATH"
echo "Creado: $DEB_PATH"
