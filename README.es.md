# Claude Usage Taskbar

[English](README.md) | [Português (Brasil)](README.pt-BR.md) | **Español** | [Русский](README.ru.md)

Consulta de un vistazo tus límites de Claude Code en la barra de tareas de Windows y descubre cuándo se agotarán o si durarán hasta el reinicio.

[![Descargar para Windows](https://img.shields.io/badge/Descargar_para_Windows-DE6E49?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/leotrvs/claude-usage-taskbar/releases/latest/download/ClaudeUsageTaskbar.exe)

![Los iconos y el panel siguiendo el uso mientras sube](docs/images/demo-en.gif)

> La aplicación está disponible en inglés y portugués. Las capturas de esta página están en inglés.

> Proyecto comunitario no oficial. No tiene relación con Anthropic, ni cuenta con su aprobación o soporte. "Claude" es una marca de Anthropic.

## Funciones

- Iconos en la barra de tareas con tu uso de **5 horas** y **semanal**. Muestra ambos o solo uno
- Tres estilos de icono: **Ring**, **Gauge** y **Needle**
- Tres plantillas de color y cuatro temas, incluido uno que sigue a Windows
- Un panel con horas de reinicio, previsión de ritmo y un gráfico de 12 horas
- Una mascota animada, con versiones pixeladas que reaccionan a tu uso
- Alertas opcionales al 80% y al 90% de un límite, y un aviso cuando un límite se renueva
- Un menú con clic derecho en los iconos, con Open panel, Refresh now, Settings y Quit
- Se inicia con Windows, con accesos directos en el Escritorio y el menú Inicio
- Actualizaciones automáticas firmadas
- **Nunca lee tu inicio de sesión ni tu token de Claude**

## Instalación

### Antes de empezar

Necesitas:

- Windows 10 u 11
- [Claude Code](https://code.claude.com/docs) instalado y con sesión iniciada en un plan Pro, Max o Ultra. Para iniciar sesión, ejecuta `claude` una vez en una terminal

No hace falta dejar una terminal abierta.

### Paso a paso

1. **Descarga** `ClaudeUsageTaskbar.exe` desde la [última versión](https://github.com/leotrvs/claude-usage-taskbar/releases/latest).
2. **Abre el archivo descargado.** Windows puede mostrar un aviso de SmartScreen, porque la aplicación no tiene un certificado de firma de código de pago. Elige **Más información** y luego **Ejecutar de todas formas**. Si lo prefieres, antes [comprueba el archivo](SECURITY.md#verify-a-download) o [compílala tú mismo](#compilar-desde-el-código).
3. **Elige Yes** en la ventana de instalación. La aplicación se instala solo para tu usuario de Windows y se conecta a Claude Code por ti. Después puedes borrar el archivo descargado.
4. **Mantén visibles los iconos.** Windows oculta los iconos nuevos de la bandeja. Arrástralos desde el menú **^** a la barra de tareas, o ve a **Configuración > Personalización > Barra de tareas > Otros iconos de la bandeja del sistema** y actívalos. Son dos iconos, uno para cada límite.
5. **Espera unos segundos.** Los porcentajes aparecen en los iconos, lo que significa que funciona.

## Uso

1. **Lee los iconos.** El límite de 5 horas tiene el número blanco y el semanal tiene el número de color. El color cambia al 70% y pasa a rojo al 90%.
2. **Pasa el ratón** sobre un icono para ver el porcentaje y la hora de reinicio.
3. **Haz clic** en un icono para abrir el panel. Haz clic fuera o pulsa **Esc** para cerrarlo. **Haz clic derecho** en un icono para ver un menú con **Open panel**, **Refresh now**, **Settings** y **Quit**.
4. **Elige un estilo de icono** en **Appearance**.
5. **Abre Settings** en el panel para todo lo demás, y **Appearance** para los colores y la mascota.

**Quit** solo cierra esta aplicación. No cierra tu sesión de Claude. Vuelve a abrirla desde el menú Inicio o el Escritorio.

## Qué puedes ver y cambiar

### Estilos de icono

![Estilos de icono](docs/images/icon-styles.gif)

Elige uno en **Appearance**. De izquierda a derecha:

- **Gauge:** un indicador con el número dentro.
- **Ring:** un círculo que se llena en sentido horario desde arriba. Es el estilo predeterminado.
- **Needle:** un indicador con aguja.

### Gráfico de uso

![El gráfico con detalles al pasar el ratón](docs/images/chart-en.gif)

El gráfico muestra tu uso de 5 horas durante las últimas 12 horas. Pasa el ratón por encima para ver la hora, el porcentaje en ese momento y cuánto subió desde la lectura anterior.

El selector **12h**, **24h** y **7d** sobre el gráfico cambia el periodo. La vista de 7 días sigue tu límite semanal. Las líneas punteadas marcan dónde un límite volvió a empezar. Debajo del gráfico, **Today** muestra el porcentaje más alto que alcanzó tu sesión de 5 horas desde medianoche.

### Previsión de ritmo

- **Límite de 5 horas:** la previsión combina tu media de toda la ventana con la tendencia reciente. Lejos del límite, un pico aislado no la domina; cerca del límite, los últimos minutos pesan más. Aparece a partir del 5% de uso y 15 minutos de datos.
- **Límite semanal:** compara tu uso con un ritmo uniforme a lo largo de la semana. Muestra una diferencia, no una predicción.

### Colores y temas

![Azul con grafito, verde con negro y morado con claro](docs/images/themes-en.gif)

En **Appearance**:

- **Colors:** azul de forma predeterminada, o verde o morado. Se aplican a los iconos y las barras.
- **Theme:** auto de forma predeterminada, o grafito, negro o claro. Auto sigue el modo de Windows: claro cuando está claro y negro cuando está oscuro.

### Mascota

De forma predeterminada, el panel muestra la mascota **Laptop** trabajando en su portátil:

<img src="assets/mascots/default.gif" alt="La mascota Laptop trabajando en su portátil" width="200">

La mascota reacciona a tu uso. Laptop usa esta animación mientras trabaja y los dibujos pixelados en los demás estados. De izquierda a derecha:

![Los estados de la mascota](docs/images/mascot-moods.gif)

| Estado | Cuándo |
|---|---|
| Durmiendo | Sin sesiones abiertas ni uso reciente, o aún sin datos |
| Descansando | Hay una sesión abierta e inactiva, o tu último uso fue hace menos de 45 minutos |
| Trabajando en su portátil | Una sesión de Claude Code está trabajando, o el porcentaje subió en los últimos 10 minutos |
| Cansada y sudando | Está trabajando y un límite está al 90% o más |
| Caída | Un límite llegó al 100% |
| Durmiendo con gorro | Igual que durmiendo, de madrugada |
| Preguntando con un signo de interrogación | Una sesión necesita tu permiso o tu respuesta |
| En pánico | La previsión indica que el límite se agotará en menos de 30 minutos |
| Tomando café | Lleva más de 2 horas trabajando |
| Malabares y pesas | Dos o más sesiones trabajando a la vez. Cuantas más sesiones, más a menudo |

También reacciona a lo que ocurre. Cuando un límite se renueva, clava una bandera de cuadros que ondea con el viento, con confeti cayendo alrededor. Cuando termina una actualización, salta y se aplasta en una lluvia de confeti. Saluda o da un salto al pasar el ratón, da un saltito de vez en cuando mientras descansa y reacciona al hacer clic. Mientras trabaja, también piensa, toma notas, lee y se estira de vez en cuando. Con dos o más sesiones trabajando, hace malabares y levanta pesas, más a menudo cuantas más sesiones haya, y al 100% a veces queda aplastada y mareada.

![Las formas de trabajar: el portátil, pensar, tomar notas, leer y estirarse](docs/images/mascot-working.gif)

![Los movimientos nuevos: salto, brinco, bandera, pesas y aplastada](docs/images/mascot-moves.gif)

En **Appearance** puedes elegir el aspecto:

- **Laptop:** la animación original mientras trabaja, y los dibujos pixelados en los demás estados. Es la predeterminada.
- **Classic:** los dibujos pixelados todo el tiempo.
- **Christmas**, **Headphones**, **Chef**, **Scientist**, **Detective**, **Winter** y **Racer:** la misma mascota con un atuendo. El Chef dice `im cooking` mientras trabaja, y el Detective a veces busca con una lupa.

![Los atuendos](docs/images/mascot-outfits.gif)

Algunos atuendos reaccionan: el Scientist remueve un frasco mientras trabaja y sus gafas parpadean en rojo en pánico, la lupa del Detective brilla cuando una sesión te espera, la bufanda del Winter ondea y el Racer agita la bandera de cuadros cuando se renueva un límite. Haz clic cinco veces seguidas en la mascota y se derrite de amor.

![Las reacciones de los atuendos y la animación de amor](docs/images/mascot-reactions.gif)

Con **Holiday outfits** activado, una mascota sin atuendo se disfraza sola en Navidad, Halloween y Carnaval. Al pasear, a veces se detiene a mirar las barras de uso si un límite está cerca, o va a buscar dónde estaba tu cursor.

Cuando descansa, la mascota da de vez en cuando un paseo corto por la parte vacía de la cabecera del panel y vuelve antes de llegar al texto.

En pánico, de vez en cuando corre de un lado a otro de la cabecera.

El Racer sale con estilo: ondea una bandera de cuadros, llega un coche de Fórmula 1, y el piloto sube, corre por la cabecera y vuelve.

![La mascota paseando por la cabecera](docs/images/mascot-walks.gif)

La mascota sin atuendo pasea con una bandera de cuadros a la espalda, y la mayoría de los atuendos tienen su propio paseo. Christmas va en trineo, Headphones y Carnival bailan, el Chef lleva una bandeja cubierta, el Scientist hace un experimento que estalla al final, Winter patina y Halloween vuela en escoba:

![Christmas, Headphones, Chef y Scientist en sus paseos](docs/images/mascot-acts-1.gif)

![Winter, Halloween y Carnival en sus paseos](docs/images/mascot-acts-2.gif)

Este es un proyecto de fans, sin relación con Anthropic ni aprobación suya, y el personaje pertenece a sus dueños. La licencia MIT no lo cubre.

### Configuración

<img src="docs/images/settings-en.gif" alt="Página Settings / Página Appearance" width="300">

| Opción | Qué hace |
|---|---|
| Language | Automático, inglés o portugués |
| Icons shown | Ambos límites, solo 5 horas o solo semanal |
| Start with Windows | Abre la aplicación al iniciar sesión en Windows |
| Automatic updates | Instala por sí sola las actualizaciones firmadas |
| Notifications | Alertas al 80% y al 90% de un límite, y avisos cuando un límite se renueva o se instala una actualización |
| Connected to Claude Code | Activa o desactiva la conexión |
| Check for updates | Busca una actualización ahora. El icono gira mientras comprueba y se convierte en un icono de descarga cuando hay una lista |

## Cómo funciona la conexión

La aplicación lee tus límites de dos formas oficiales:

- **Comando `/usage`:** cada 2 minutos, y al abrir el panel, ejecuta `claude -p /usage` en segundo plano, con los mismos números que ves en Claude Code. No llama al modelo ni gasta tokens.
- **[Status line](https://code.claude.com/docs/en/statusline):** durante la instalación, añade una entrada `statusLine` a `~/.claude/settings.json` y guarda una copia de seguridad al lado. Claude Code envía entonces números nuevos con cada respuesta en una terminal.

En ambos casos, la aplicación guarda solo los porcentajes y las horas de reinicio de los límites de 5 horas y semanal en `%LOCALAPPDATA%\ClaudeUsageTaskbar`, y descarta el resto. El widget lee ese archivo.

Conviene saber:

- **Cuenta el uso desde cualquier lugar.** claude.ai, la aplicación de escritorio y las terminales aparecen en un máximo de 2 minutos.
- **Si ya tienes una status line, se conserva.** Sigue ejecutándose y mostrando lo mismo que antes. Al desinstalar, se restaura.
- **Para desconectar,** desactiva **Connected to Claude Code** en Settings.

## Seguridad y privacidad

- No lee tu inicio de sesión, tu token ni tu archivo de credenciales.
- No guarda tus prompts, datos de sesión ni rutas de proyectos.
- No tiene analíticas, telemetría ni cuentas.
- El acceso a la red de la propia aplicación es solo comprobar en GitHub si hay actualizaciones firmadas, y puedes desactivarlo. El comando `/usage` es de Claude Code, que usa tu inicio de sesión sin mostrárselo a la aplicación.

Los detalles están en [SECURITY.md](SECURITY.md), en inglés.

## Actualizaciones

Al abrirse y después cada hora, la aplicación comprueba la última versión en GitHub. Solo instala una actualización si su firma digital coincide con la clave integrada en la aplicación, así que un archivo manipulado se rechaza. Puedes desactivarlo en Settings, donde **Check for updates** también hace una comprobación al momento. La comprobación al abrirse instala la actualización de inmediato, sin preguntar. Las comprobaciones siguientes esperan a que se cierre el panel y, mientras tanto, muestran un enlace para instalarla. Una notificación confirma la actualización, salvo que desactives **Notifications**.

## Desinstalación

Ve a **Configuración de Windows > Aplicaciones > Aplicaciones instaladas > Claude Usage Taskbar > Desinstalar**. Esto elimina la aplicación, los accesos directos y los ajustes, y restaura tu status line de Claude Code.

## Solución de problemas

| Problema | Qué hacer |
|---|---|
| "Waiting for Claude Code" | Espera unos segundos. Si continúa, ejecuta `claude` en una terminal para comprobar que Claude Code está instalado y con la sesión iniciada. |
| "Claude Code not found" | Instala [Claude Code](https://code.claude.com/docs) y ejecuta `claude` en una terminal para iniciar sesión. |
| Siguen sin aparecer los porcentajes | Comprueba que **Connected to Claude Code** esté activado y ejecuta `claude -p /usage` en una terminal. Los datos están disponibles en los planes Pro, Max y Ultra. |
| Los iconos están ocultos | Sigue el paso 4 del [paso a paso](#paso-a-paso). |
| No se pudo activar la conexión | `~/.claude/settings.json` puede tener un JSON no válido. Corrígelo e inténtalo de nuevo. |
| "Out of date" en amarillo | Las últimas lecturas fallaron, así que los números pueden ser antiguos. La aplicación lo intenta de nuevo cada 2 minutos. |

## Compilar desde el código

No hace falta ningún SDK, porque Windows incluye el compilador de C#.

```powershell
git clone https://github.com/leotrvs/claude-usage-taskbar.git
cd claude-usage-taskbar
pwsh -File scripts/build.ps1
```

La aplicación se genera en `bin\ClaudeUsageTaskbar.exe`. Usa `--portable` para ejecutarla sin instalar.

Si quieres contribuir, lee [CONTRIBUTING.md](CONTRIBUTING.md).

## Créditos

El panel usa [Inter](https://github.com/rsms/inter), con licencia SIL Open Font License. Consulta [assets/fonts/NOTICE.md](assets/fonts/NOTICE.md).

## Licencia

[MIT](LICENSE)
