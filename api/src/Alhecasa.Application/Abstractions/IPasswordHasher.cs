namespace Alhecasa.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);

    /// <summary>
    /// Ejecuta una verificación contra un hash ficticio. Se usa cuando el usuario no existe
    /// para igualar el tiempo de respuesta y no revelar qué correos están registrados.
    /// </summary>
    void VerifyDummy(string password);
}
