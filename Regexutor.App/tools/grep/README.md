# grep.exe (dependencia portable)

Regexutor evalúa tus expresiones regulares con **grep (POSIX BRE/ERE)**.

Para que la app sea **100% portable** (funcione en otro equipo sin instalar Git), coloca un `grep.exe` aquí:

`Regexutor.App/tools/grep/grep.exe`

La app lo buscará en este orden:

1. Variable de entorno `REGEXUTOR_GREP` (ruta completa a `grep.exe`)
2. Junto al ejecutable: `.\grep.exe`
3. En esta carpeta: `.\tools\grep\grep.exe`
4. En el `PATH` del sistema

Nota: asegúrate de cumplir la licencia del `grep.exe` que distribuyas.

