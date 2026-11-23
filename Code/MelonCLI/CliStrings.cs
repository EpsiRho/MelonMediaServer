using Melon.Controllers;

namespace MelonCLI;

internal static class CliStrings
{
    private static readonly Dictionary<string, string> Spanish = new(StringComparer.Ordinal)
    {
        ["Terminal default"] = "Predeterminado del terminal", ["Library folder"] = "Carpeta de biblioteca",
        ["Connecting…"] = "Conectando…", ["Confirm"] = "Confirmar", ["Back"] = "Volver", ["Error"] = "Error",
        ["Backup"] = "Copia", ["Save as"] = "Guardar como", ["Replace file?"] = "¿Reemplazar archivo?",
        ["No entries."] = "No hay entradas.", ["Enable"] = "Activar", ["Disable"] = "Desactivar", ["HTTP port"] = "Puerto HTTP",
        ["Keep current connection"] = "Mantener conexión actual", ["Install MongoDB on this PC?"] = "¿Instalar MongoDB en este equipo?",
        ["Plugin unavailable."] = "Complemento no disponible.", ["Path"] = "Ruta",

        // Frame, footer and prompts
        ["Actions"] = "Acciones", ["Sections"] = "Secciones", ["Move"] = "Mover", ["Select"] = "Elegir", ["Scroll"] = "Desplazar",
        ["Quit"] = "Salir", ["Open"] = "Abrir", ["Select folder"] = "Elegir carpeta", ["Open or select"] = "Abrir o elegir",
        ["Type a folder path, or press Tab to browse."] = "Escribe la ruta de una carpeta o pulsa Tab para explorar.",
        ["Type a file path, or press Tab to browse."] = "Escribe la ruta de un archivo o pulsa Tab para explorar.",
        ["Adjust each channel, or press Tab to type a hex value."] = "Ajusta cada canal o pulsa Tab para escribir un valor hexadecimal.",
        ["Channel"] = "Canal", ["Adjust"] = "Ajustar", ["Hex"] = "Hex", ["New line"] = "Nueva línea", ["Reveal"] = "Mostrar",
        ["This cannot be undone."] = "No se puede deshacer.", ["Something went wrong"] = "Algo salió mal",
        ["Starting Melon…"] = "Iniciando Melon…", ["Where is Melon installed?"] = "¿Dónde está instalado Melon?",
        ["The folder with the Server folder inside it. You only need to do this once."] = "La carpeta que tiene la carpeta Server dentro. Solo hace falta una vez.",
        ["Melon's server wasn't found in that folder."] = "No se encontró el servidor de Melon en esa carpeta.", ["Online"] = "En línea", ["Database offline"] = "Base de datos sin conexión",
        ["Scanning"] = "Escaneando", ["Restart required"] = "Reinicio necesario", ["Melon restarted."] = "Melon se reinició.",
        ["Start Melon to manage your library, users and settings from here."] = "Inicia Melon para gestionar la biblioteca, los usuarios y los ajustes desde aquí.",
        ["Action needed"] = "Acción necesaria", ["Pending conversions"] = "Conversiones pendientes",
        ["The database needs to be converted before Melon can use it."] = "La base de datos debe convertirse antes de que Melon pueda usarla.",
        ["Welcome"] = "Bienvenida", ["Let’s get Melon ready"] = "Preparemos Melon",
        ["Create the first admin account, choose your library folders, and connect MongoDB."] = "Crea la primera cuenta de administrador, elige las carpetas de la biblioteca y conecta MongoDB.",
        ["Library, users and stats are unavailable until the database reconnects."] = "La biblioteca, los usuarios y las estadísticas no están disponibles hasta que la base de datos se vuelva a conectar.",
        ["Some settings take effect after Melon restarts."] = "Algunos ajustes se aplican cuando Melon se reinicia.",

        // Library and database
        ["Your music collection"] = "Tu colección de música", ["folder"] = "carpeta", ["folders"] = "carpetas",
        ["songs each"] = "canciones cada uno", ["albums each"] = "álbumes cada uno", ["No library folders yet"] = "Aún no hay carpetas",
        ["Add a folder so Melon knows where your music lives."] = "Añade una carpeta para que Melon sepa dónde está tu música.",
        ["new files"] = "archivos nuevos", ["everything"] = "todo", ["Folder removed."] = "Carpeta quitada.", ["Folder added."] = "Carpeta añadida.",
        ["Scan started."] = "Escaneo iniciado.", ["Saved."] = "Guardado.", ["Status"] = "Estado", ["Disconnected"] = "Desconectado",
        ["Latest"] = "Última", ["None yet"] = "Ninguna todavía", ["Queue cleanup"] = "Limpieza de colas", ["Off"] = "Desactivada",
        ["Removes stale play queues"] = "Elimina colas antiguas", ["Connection"] = "Conexión", ["Backup restored."] = "Copia restaurada.",
        ["Transfer"] = "Transferencia", ["Danger zone"] = "Zona de peligro",

        // Networking, plugins, system
        ["How clients reach Melon"] = "Cómo se conectan los clientes", ["Traffic is not encrypted"] = "El tráfico no está cifrado",
        ["Address"] = "Dirección", ["Security"] = "Seguridad", ["Extend Melon"] = "Amplía Melon", ["Installed"] = "Instalados",
        ["Drop plugin folders into the Plugins folder, then rescan."] = "Copia los complementos en la carpeta Plugins y vuelve a escanear.",
        ["Plugins rescanned."] = "Complementos escaneados.", ["Plugins reloaded."] = "Complementos recargados.", ["Folder"] = "Carpeta",
        ["Version"] = "Versión", ["Author"] = "Autor", ["About"] = "Acerca de", ["Settings"] = "Ajustes", ["Not checked"] = "Sin comprobar",
        ["Environment"] = "Entorno", ["Data folder"] = "Carpeta de datos", ["Platform"] = "Plataforma", ["Preferences"] = "Preferencias",
        ["Release notes"] = "Notas de la versión", ["Server"] = "Servidor",

        // Users
        ["Accounts and invitations"] = "Cuentas e invitaciones", ["Next expires"] = "La próxima caduca",
        ["Anyone with the code can create an account until it expires."] = "Cualquiera con el código puede crear una cuenta hasta que caduque.",
        ["User deleted."] = "Usuario eliminado.", ["Create a new one to try again."] = "Crea otro para volver a intentarlo.",
        ["Scan the QR code or share the code to create an account."] = "Escanea el código QR o comparte el código para crear una cuenta.",
        ["Invite revoked."] = "Invitación revocada.",

        // Stats
        ["vs previous"] = "vs. anterior", ["pts"] = "pts", ["new"] = "nuevo", ["day"] = "día", ["days"] = "días",
        ["Range"] = "Rango", ["Updated"] = "Actualizado", ["Skip rate"] = "Tasa de omisión", ["Daily average"] = "Media diaria",
        ["plays"] = "reproducciones", ["Active days"] = "Días activos", ["Streak"] = "Racha", ["Time"] = "Tiempo", ["Peak"] = "Pico",
        ["Try a longer period or another user."] = "Prueba un periodo más largo u otro usuario.", ["per week"] = "por semana", ["per day"] = "por día",
        ["No plays yet."] = "Aún no hay reproducciones.", ["share of plays"] = "porcentaje", ["By weekday"] = "Por día de la semana",
        ["plays · avg"] = "reproducciones · media", ["Best day"] = "Mejor día", ["Longest streak"] = "Racha más larga",
        ["Current streak"] = "Racha actual", ["Longest break"] = "Pausa más larga", ["Busiest weekday"] = "Día con más actividad",
        ["of plays"] = "de las reproducciones", ["Average play"] = "Reproducción media", ["Plays per active day"] = "Reproducciones por día activo",
        ["Previous period"] = "Periodo anterior", ["Highlights"] = "Destacados", ["Calendar"] = "Calendario", ["Less"] = "Menos", ["More"] = "Más",
        ["active days"] = "días activos", ["Filters"] = "Filtros", ["Explore"] = "Explorar", ["Daily breakdown"] = "Desglose diario",
        ["Bars average {0} days"] = "Barras: media de {0} días", ["Bars average {0} weeks"] = "Barras: media de {0} semanas",
        ["14 days"] = "14 días", ["180 days"] = "180 días", ["365 days"] = "365 días", ["Date"] = "Fecha", ["Day"] = "Día",
        ["Press Esc again to quit"] = "Pulsa Esc otra vez para salir", ["Change listening URL"] = "Cambiar URL de escucha",
        ["Open data folder"] = "Abrir carpeta de datos", ["Update available"] = "Actualización disponible",

        // Theme editor
        ["Controller theme"] = "Tema del controlador", ["Presets"] = "Predefinidos", ["Custom"] = "Personalizado",
        ["Customize colors"] = "Personalizar colores", ["Colors"] = "Colores", ["Start from"] = "Partir de", ["Palette"] = "Paleta",
        ["Sample"] = "Muestra", ["Melon Dark"] = "Melon oscuro", ["Melon Light"] = "Melon claro", ["Midnight"] = "Medianoche",
        ["Ember"] = "Brasa", ["Mono"] = "Mono", ["Background"] = "Fondo", ["Surface"] = "Superficie", ["Raised surface"] = "Superficie elevada",
        ["Border"] = "Borde", ["Text"] = "Texto", ["Muted text"] = "Texto atenuado", ["Faint text"] = "Texto tenue",
        ["Accent"] = "Acento", ["Text on accent"] = "Texto sobre acento", ["Secondary"] = "Secundario", ["Positive"] = "Positivo",
        ["Warning"] = "Advertencia", ["Danger"] = "Peligro", ["Info"] = "Información",
    };

    public static string Get(string language, string key)
    {
        if (!language.Split('-')[0].Equals("ES", StringComparison.OrdinalIgnoreCase)) return key;
        return Spanish.TryGetValue(key, out var value) ? value : UiStrings.Get(language, key);
    }
}
