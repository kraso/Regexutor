# Regexutor ©

App de escritorio para practicar expresiones regulares **POSIX BRE/ERE** con `grep`.

## Requisitos

- Windows
- Git for Windows (para `grep.exe`) o cualquier `grep.exe` en el `PATH`

## Ejecutar

Abre la solución `Regexutor.sln` en Visual Studio y ejecuta el proyecto `Regexutor.App`.

## Linux (paquete `.deb`)

La interfaz **WPF** solo funciona en Windows. En Linux se ofrece **`regexutor-cli`**, el mismo motor POSIX vía `grep`.

### Generar el `.deb`

Hace falta **Linux**, **WSL2** o **Docker** (no se puede ejecutar `dpkg-deb` solo con Windows nativo).

Desde la raíz del repositorio:

```bash
chmod +x packaging/linux/build-deb.sh
./packaging/linux/build-deb.sh
```

El artefacto queda en `publish/regexutor-cli_<versión>_amd64.deb` (variable opcional `VERSION=1.0.0`).

Con **Docker** (sin instalar la cadena Debian en la máquina):

```bash
docker build -f packaging/linux/Dockerfile -t regexutor-deb .
docker create --name regexutor-deb-out regexutor-deb
docker cp regexutor-deb-out:/artifacts ./publish/docker-deb
docker rm regexutor-deb-out
```

Instalación en Debian/Ubuntu: `sudo dpkg -i publish/regexutor-cli_*_amd64.deb` (requiere `grep`; el paquete lo declara en `Depends`).

### Prueba rápida (copiar y pegar)

El comando correcto **siempre** empieza por `regexutor-cli eval` o `regexutor-cli grep-path`. Si solo escribes `regexutor-cli`, verás la ayuda y código de salida 2.

1. Comprobar que encuentra `grep`:

```bash
regexutor-cli grep-path
```

Debe imprimir algo como `/usr/bin/grep`. Si falla: `sudo apt install grep` o `export REGEXUTOR_GREP=/ruta/al/grep`.

2. Crear un archivo de casos con **TAB real** entre columnas (los espacios **no** sirven):

```bash
printf 'aaa\ttrue\nbbb\tfalse\na\tfalse\n' > /tmp/casos.tsv
```

3. Ejecutar la evaluación (entrecomillar el patrón si el shell interpreta `$`, `` ` ``, `!`, etc.):

```bash
regexutor-cli eval --dialect ere --pattern '^a+$' --cases /tmp/casos.tsv
```

Salida: una línea por caso (`OK` o `FAIL`) y código de salida **0** si todos pasan, **1** si alguno falla. En este ejemplo las dos primeras filas son `OK` y la tercera `FAIL` (se esperaba `false` pero `a` sí cumple `^a+$`).

Si tienes el repositorio clonado, puedes usar el ejemplo incluido (incluye esa tercera línea deliberadamente incorrecta):

```bash
regexutor-cli eval --dialect ere --pattern '^a+$' --cases packaging/linux/examples/demo-casos.tsv
```

### Uso del CLI

```bash
regexutor-cli --version
regexutor-cli grep-path
regexutor-cli eval --dialect ere --pattern '^a+$' --cases casos.tsv
```

Formato de `casos.tsv`: una línea por prueba, `entrada<TAB>true|false` (líneas que empiezan por `#` se ignoran).

