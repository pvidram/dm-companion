namespace DmCompanion.UI.Components;

/// <summary>
/// Aviso efímero para <see cref="Toast"/>. Toast compara por referencia: crear una instancia nueva, aunque tenga el mismo
/// texto, muestra el aviso otra vez y reinicia el tiempo; pasar la misma instancia no hace nada.
/// </summary>
/// <param name="Text">Texto del aviso.</param>
/// <param name="ActionLabel">Texto del botón de acción (p. ej. «Deshacer»); sin él no hay botón.</param>
/// <param name="Duration">Tiempo visible; por defecto <see cref="Toast.DefaultDuration"/>.</param>
/// <param name="ActionIcon">Icono opcional del botón de acción (p. ej. <see cref="IconName.Undo2"/>).</param>
public sealed record ToastMessage(string Text, string? ActionLabel = null, TimeSpan? Duration = null, IconName? ActionIcon = null);
