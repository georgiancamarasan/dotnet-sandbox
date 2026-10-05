namespace CultureSandbox;

/// <summary>
/// Snapshot of how the culture was resolved for the HTTP request that started the circuit.
/// Captured in App.razor (the only place HttpContext exists) and passed into the interactive Routes component.
/// Must be JSON-serializable because it crosses into the interactive render mode as a root component parameter.
/// </summary>
public record InitialRequestInfo(
    string Culture,
    string UICulture,
    string? Provider,
    string? UserId);
