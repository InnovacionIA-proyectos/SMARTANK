namespace Smartank.Api.Tests.Infra;

/// <summary>
/// Fact que solo corre cuando existe la variable de entorno <c>TEST_DB_CONNECTION</c> (cadena de conexion a una base
/// SQL Server 2019 vacia donde ya se ejecuto Database/ddl.sql). Sin ella la prueba se omite, de modo que
/// <c>dotnet test</c> pasa en maquinas y pipelines sin base de pruebas.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class clsHechoConBaseDePruebasAttribute : FactAttribute
{
    public const string VARIABLE_ENTORNO = "TEST_DB_CONNECTION";

    public clsHechoConBaseDePruebasAttribute()
    {
        if (string.IsNullOrWhiteSpace(CadenaDeConexion))
            Skip = $"Defina la variable de entorno {VARIABLE_ENTORNO} para ejecutar las pruebas contra SQL Server.";
    }

    /// <summary>Cadena de conexion de la base de pruebas, o null si no esta definida.</summary>
    public static string? CadenaDeConexion => Environment.GetEnvironmentVariable(VARIABLE_ENTORNO);
}
