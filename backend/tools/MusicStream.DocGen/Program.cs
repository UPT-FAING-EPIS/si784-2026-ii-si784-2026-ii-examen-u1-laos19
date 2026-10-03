using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MusicStream.Api.Data;

namespace MusicStream.DocGen;

/// <summary>
/// Genera la documentación técnica (diccionario de datos y diagramas Mermaid) a partir del código:
/// modelo de EF Core, ensamblado de la API, páginas del frontend y archivos Terraform.
/// Uso: dotnet run --project backend/tools/MusicStream.DocGen -- [carpetaRaizRepo] [carpetaSalida]
/// </summary>
public static class Program
{
    private static readonly Assembly ApiAssembly = typeof(MusicStreamDbContext).Assembly;

    public static int Main(string[] args)
    {
        var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
        var output = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(root, "docs"));
        Directory.CreateDirectory(Path.Combine(output, "database"));
        Directory.CreateDirectory(Path.Combine(output, "architecture"));

        var options = new DbContextOptionsBuilder<MusicStreamDbContext>().UseNpgsql("Host=localhost").Options;
        using var context = new MusicStreamDbContext(options);
        var model = context.GetService<IDesignTimeModel>().Model;

        Write(output, "database/data-dictionary.md", DataDictionary(model));
        Write(output, "database/er-diagram.md", ErDiagram(model));
        Write(output, "architecture/class-diagram.md", ClassDiagram());
        Write(output, "architecture/component-diagram.md", ComponentDiagram(root));
        Write(output, "architecture/deployment-diagram.md", DeploymentDiagram(root));
        Write(output, "README.md", Index());
        Console.WriteLine($"Documentación generada en {output}");
        return 0;
    }

    private static void Write(string output, string relative, string content)
    {
        var path = Path.Combine(output, relative);
        File.WriteAllText(path, content.ReplaceLineEndings("\n"));
        Console.WriteLine($"  - {relative}");
    }

    private static string Header(string title) =>
        $"# {title}\n\n> Documento generado automáticamente por `MusicStream.DocGen` (workflow `generate-documentation.yml`). No editar manualmente.\n\n";

    // ------------------------------------------------------------------ Base de datos

    private static IEnumerable<IEntityType> Tables(IModel model) =>
        model.GetEntityTypes().Where(e => e.GetTableName() is not null).OrderBy(e => e.GetTableName());

    internal static string DataDictionary(IModel model)
    {
        var sb = new StringBuilder(Header("Diccionario de datos"));
        sb.AppendLine("Motor de base de datos: **PostgreSQL 16** (producción) / SQLite (desarrollo y pruebas).\n");
        sb.AppendLine("| Tabla | Entidad | Columnas |");
        sb.AppendLine("|---|---|---|");
        foreach (var entity in Tables(model))
        {
            sb.AppendLine($"| [`{entity.GetTableName()}`](#{entity.GetTableName()}) | {entity.ClrType.Name} | {entity.GetProperties().Count()} |");
        }

        foreach (var entity in Tables(model))
        {
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            sb.AppendLine($"\n## {entity.GetTableName()}\n");
            sb.AppendLine($"Entidad: `{entity.ClrType.FullName}`\n");
            sb.AppendLine("| Columna | Tipo | Nulo | PK | FK | Único | Longitud / Precisión | Descripción |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            var pk = entity.FindPrimaryKey();
            foreach (var p in entity.GetProperties())
            {
                var fk = p.GetContainingForeignKeys().FirstOrDefault();
                var unique = entity.GetIndexes().Any(i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0] == p);
                var size = p.GetMaxLength()?.ToString() ?? (p.GetPrecision() is { } prec ? $"{prec},{p.GetScale()}" : "—");
                var fkText = fk is null ? "" : $"→ `{fk.PrincipalEntityType.GetTableName()}.{fk.PrincipalKey.Properties[0].GetColumnName(table)}`";
                sb.AppendLine($"| `{p.GetColumnName(table)}` | `{p.GetColumnType()}` | {(p.IsNullable ? "Sí" : "No")} | {(pk?.Properties.Contains(p) == true ? "✔" : "")} | {fkText} | {(unique ? "✔" : "")} | {size} | {Describe(p)} |");
            }

            var indexes = entity.GetIndexes().ToList();
            if (indexes.Count > 0)
            {
                sb.AppendLine("\n**Índices**\n");
                foreach (var index in indexes)
                {
                    sb.AppendLine($"- `{index.GetDatabaseName(table)}` ({string.Join(", ", index.Properties.Select(x => x.GetColumnName(table)))}){(index.IsUnique ? " — único" : "")}");
                }
            }

            var fks = entity.GetForeignKeys().ToList();
            if (fks.Count > 0)
            {
                sb.AppendLine("\n**Relaciones**\n");
                foreach (var fk in fks)
                {
                    sb.AppendLine($"- `{string.Join(", ", fk.Properties.Select(x => x.GetColumnName(table)))}` → `{fk.PrincipalEntityType.GetTableName()}` (ON DELETE {fk.DeleteBehavior.ToString().ToUpperInvariant()})");
                }
            }
        }

        return sb.ToString();
    }

    private static string Describe(IProperty p)
    {
        if (p.IsPrimaryKey()) return "Identificador único";
        if (p.IsForeignKey()) return $"Referencia a {p.GetContainingForeignKeys().First().PrincipalEntityType.ClrType.Name}";
        return Humanize(p.Name);
    }

    private static string Humanize(string name) => Regex.Replace(name, "(?<!^)([A-Z])", " $1", RegexOptions.None, TimeSpan.FromMilliseconds(100)).ToLowerInvariant();

    internal static string ErDiagram(IModel model)
    {
        var sb = new StringBuilder(Header("Diagrama entidad-relación"));
        sb.AppendLine("```mermaid");
        sb.AppendLine("erDiagram");
        foreach (var entity in Tables(model))
        {
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            sb.AppendLine($"    {entity.GetTableName()} {{");
            foreach (var p in entity.GetProperties())
            {
                var type = MermaidType(p.GetColumnType());
                var keys = new List<string>();
                if (p.IsPrimaryKey()) keys.Add("PK");
                if (p.IsForeignKey()) keys.Add("FK");
                if (entity.GetIndexes().Any(i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0] == p)) keys.Add("UK");
                sb.AppendLine($"        {type} {p.GetColumnName(table)}{(keys.Count > 0 ? " " + string.Join(",", keys) : "")}");
            }

            sb.AppendLine("    }");
        }

        foreach (var entity in Tables(model))
        {
            foreach (var fk in entity.GetForeignKeys())
            {
                var left = fk.IsRequired ? "||" : "|o";
                var right = fk.IsUnique ? "o|" : "o{";
                sb.AppendLine($"    {fk.PrincipalEntityType.GetTableName()} {left}--{right} {entity.GetTableName()} : \"{string.Join(",", fk.Properties.Select(x => x.Name))}\"");
            }
        }

        sb.AppendLine("```");
        return sb.ToString();
    }

    /// <summary>Normaliza el tipo SQL a un identificador válido para Mermaid (sin espacios ni comas).</summary>
    internal static string MermaidType(string columnType)
    {
        var type = columnType
            .Replace("character varying", "varchar", StringComparison.OrdinalIgnoreCase)
            .Replace("timestamp with time zone", "timestamptz", StringComparison.OrdinalIgnoreCase);
        type = Regex.Replace(type, @"\(\d+,\s*\d+\)", string.Empty, RegexOptions.None, TimeSpan.FromMilliseconds(100));
        return Regex.Replace(type, @"[^A-Za-z0-9_()]", "_", RegexOptions.None, TimeSpan.FromMilliseconds(100));
    }

    // ------------------------------------------------------------------ Clases

    internal static string ClassDiagram()
    {
        var sb = new StringBuilder(Header("Diagrama de clases"));
        var domain = ApiAssembly.GetTypes().Where(t => t.Namespace == "MusicStream.Api.Domain" && t.IsPublic).OrderBy(t => t.Name).ToList();
        var services = ApiAssembly.GetTypes().Where(t => t.Namespace == "MusicStream.Api.Services" && t.IsPublic && t.IsInterface).OrderBy(t => t.Name).ToList();
        var controllers = ControllerTypes().ToList();

        sb.AppendLine("## Dominio\n");
        sb.AppendLine("```mermaid");
        sb.AppendLine("classDiagram");
        foreach (var type in domain)
        {
            if (type.IsEnum)
            {
                sb.AppendLine($"    class {type.Name} {{");
                sb.AppendLine("        <<enumeration>>");
                foreach (var name in Enum.GetNames(type)) sb.AppendLine($"        {name}");
                sb.AppendLine("    }");
                continue;
            }

            sb.AppendLine($"    class {type.Name} {{");
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => !IsNavigation(p.PropertyType, domain)))
            {
                sb.AppendLine($"        +{TypeName(prop.PropertyType)} {prop.Name}");
            }

            sb.AppendLine("    }");
        }

        foreach (var type in domain.Where(t => !t.IsEnum))
        {
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var element = CollectionElement(prop.PropertyType);
                if (element is not null && domain.Contains(element))
                {
                    sb.AppendLine($"    {type.Name} \"1\" --> \"*\" {element.Name} : {prop.Name}");
                }
                else if (domain.Contains(prop.PropertyType) && prop.PropertyType.IsEnum)
                {
                    sb.AppendLine($"    {type.Name} ..> {prop.PropertyType.Name}");
                }
            }
        }

        sb.AppendLine("```\n");

        sb.AppendLine("## Servicios y controladores\n");
        sb.AppendLine("```mermaid");
        sb.AppendLine("classDiagram");
        foreach (var service in services)
        {
            sb.AppendLine($"    class {service.Name} {{");
            sb.AppendLine("        <<interface>>");
            foreach (var m in service.GetMethods()) sb.AppendLine($"        +{m.Name}()");
            sb.AppendLine("    }");
            foreach (var impl in ApiAssembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && service.IsAssignableFrom(t)))
            {
                sb.AppendLine($"    class {impl.Name}");
                sb.AppendLine($"    {service.Name} <|.. {impl.Name}");
                foreach (var dep in Dependencies(impl).Where(d => d.IsInterface && services.Contains(d)))
                {
                    sb.AppendLine($"    {impl.Name} --> {dep.Name}");
                }

                if (Dependencies(impl).Any(d => d == typeof(MusicStreamDbContext)))
                {
                    sb.AppendLine($"    {impl.Name} --> MusicStreamDbContext");
                }
            }
        }

        sb.AppendLine("    class MusicStreamDbContext {\n        <<DbContext>>\n    }");
        foreach (var controller in controllers)
        {
            sb.AppendLine($"    class {controller.Name} {{");
            sb.AppendLine("        <<controller>>");
            foreach (var m in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) sb.AppendLine($"        +{m.Name}()");
            sb.AppendLine("    }");
            foreach (var dep in Dependencies(controller).Where(d => d.IsInterface))
            {
                sb.AppendLine($"    {controller.Name} --> {dep.Name}");
            }
        }

        sb.AppendLine("```");
        return sb.ToString();
    }

    private static IEnumerable<Type> ControllerTypes() =>
        ApiAssembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract).OrderBy(t => t.Name);

    private static IEnumerable<Type> Dependencies(Type type) =>
        type.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType).Distinct();

    private static Type? CollectionElement(Type type) =>
        type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string)
            ? type.GetGenericArguments()[0]
            : null;

    private static bool IsNavigation(Type type, List<Type> domain) =>
        (domain.Contains(type) && !type.IsEnum) || (CollectionElement(type) is { } e && domain.Contains(e));

    private static string TypeName(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null) return TypeName(underlying) + "?";
        return type.Name switch
        {
            "String" => "string",
            "Int32" => "int",
            "Boolean" => "bool",
            "Decimal" => "decimal",
            _ => type.Name
        };
    }

    // ------------------------------------------------------------------ Componentes

    internal static string ComponentDiagram(string root)
    {
        var sb = new StringBuilder(Header("Diagrama de componentes"));
        var pagesDir = Path.Combine(root, "frontend", "src", "pages");
        var pages = Directory.Exists(pagesDir)
            ? Directory.GetFiles(pagesDir, "*.jsx").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x).ToList()
            : new List<string?>();

        sb.AppendLine("```mermaid");
        sb.AppendLine("flowchart LR");
        sb.AppendLine("    user([Usuario / Administrador])");
        sb.AppendLine("    subgraph FE[\"Frontend SPA - React + Vite\"]");
        sb.AppendLine("        router[React Router]");
        sb.AppendLine("        ctx[AppContext<br/>sesión JWT y reproductor]");
        sb.AppendLine("        apiClient[lib/api.js<br/>cliente REST]");
        sb.AppendLine("        validation[lib/validation.js]");
        foreach (var page in pages) sb.AppendLine($"        {page}[{page}]");
        sb.AppendLine("    end");
        sb.AppendLine("    subgraph BE[\"Backend - ASP.NET Core 8 Web API (contenedor)\"]");
        sb.AppendLine("        auth[JWT Bearer + Rate limiting]");
        foreach (var controller in ControllerTypes())
        {
            var route = controller.GetCustomAttribute<RouteAttribute>()?.Template;
            var routes = route is not null
                ? "/" + route
                : string.Join(", ", controller.GetMethods().SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>()).Select(a => "/" + a.Template?.Split('/')[0]).Distinct());
            var secured = controller.GetCustomAttribute<AuthorizeAttribute>() is { } a ? $" 🔒{a.Roles}" : "";
            sb.AppendLine($"        {controller.Name}[\"{controller.Name}<br/>{routes}{secured}\"]");
        }

        foreach (var service in ApiAssembly.GetTypes().Where(t => t.Namespace == "MusicStream.Api.Services" && t.IsInterface && t.IsPublic).OrderBy(t => t.Name))
        {
            sb.AppendLine($"        {service.Name}{{{{{service.Name}}}}}");
        }

        sb.AppendLine("        efcore[(EF Core<br/>MusicStreamDbContext)]");
        sb.AppendLine("    end");
        sb.AppendLine("    db[(PostgreSQL)]");
        sb.AppendLine("    cdn[[CDN de audio HTTPS]]");
        sb.AppendLine("    user --> router");
        foreach (var page in pages) sb.AppendLine($"    router --> {page}");
        foreach (var page in pages) sb.AppendLine($"    {page} --> ctx");
        sb.AppendLine("    ctx --> apiClient");
        sb.AppendLine("    ctx --> validation");
        sb.AppendLine("    apiClient -- HTTPS/JSON --> auth");
        foreach (var controller in ControllerTypes())
        {
            sb.AppendLine($"    auth --> {controller.Name}");
            foreach (var dep in Dependencies(controller).Where(d => d.IsInterface)) sb.AppendLine($"    {controller.Name} --> {dep.Name}");
        }

        foreach (var service in ApiAssembly.GetTypes().Where(t => t.Namespace == "MusicStream.Api.Services" && t.IsInterface && t.IsPublic))
        {
            sb.AppendLine($"    {service.Name} --> efcore");
        }

        sb.AppendLine("    efcore --> db");
        sb.AppendLine("    ctx -. streaming de audio .-> cdn");
        sb.AppendLine("```");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ Despliegue

    internal static string DeploymentDiagram(string root)
    {
        var sb = new StringBuilder(Header("Diagrama de despliegue"));
        var tfDir = Path.Combine(root, "infra", "terraform");
        var resources = new List<(string Type, string Name)>();
        if (Directory.Exists(tfDir))
        {
            var regex = new Regex("^resource\\s+\"(?<type>[a-z0-9_]+)\"\\s+\"(?<name>[a-z0-9_]+)\"", RegexOptions.Multiline, TimeSpan.FromSeconds(1));
            foreach (var file in Directory.GetFiles(tfDir, "*.tf"))
            {
                resources.AddRange(regex.Matches(File.ReadAllText(file)).Select(m => (m.Groups["type"].Value, m.Groups["name"].Value)));
            }
        }

        bool Has(string type) => resources.Any(r => r.Type == type);

        sb.AppendLine("Recursos declarados en Terraform (`infra/terraform`):\n");
        foreach (var (type, name) in resources.OrderBy(r => r.Type)) sb.AppendLine($"- `{type}.{name}`");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("flowchart TB");
        sb.AppendLine("    dev([Desarrollador]) -->|git push| gh[GitHub Repository]");
        sb.AppendLine("    gh -.OIDC sin secretos.-> AZ");
        sb.AppendLine("    subgraph GHA[\"GitHub Actions\"]");
        sb.AppendLine("        infra[infra.yml<br/>Terraform]");
        sb.AppendLine("        deploy[deploy.yml]");
        sb.AppendLine("        sonar[sonar.yml]");
        sb.AppendLine("        sec[snyk-semgrep.yml]");
        sb.AppendLine("        docs[generate-documentation.yml]");
        sb.AppendLine("    end");
        sb.AppendLine("    gh --> GHA");
        sb.AppendLine("    sonar --> sc[(SonarQube efímero<br/>o SonarCloud)]");
        sb.AppendLine("    sec --> snyk[(Semgrep / Trivy<br/>Snyk opcional)]");
        sb.AppendLine("    browser([Navegador del usuario])");
        sb.AppendLine("    subgraph AZ[\"Microsoft Azure - Resource Group\"]");
        if (Has("azurerm_static_web_app")) sb.AppendLine("        swa[Azure Static Web Apps<br/>Frontend React]");
        if (Has("azurerm_container_registry")) sb.AppendLine("        acr[(Azure Container Registry<br/>musicstream-api)]");
        if (Has("azurerm_virtual_network"))
        {
            sb.AppendLine("        subgraph VNET[\"Virtual Network 10.20.0.0/16\"]");
            sb.AppendLine("            subgraph SAPP[\"snet-app\"]");
        }

        if (Has("azurerm_linux_web_app")) sb.AppendLine("                app[App Service Linux<br/>contenedor .NET 8 distroless<br/>Managed Identity]");
        if (Has("azurerm_virtual_network")) sb.AppendLine("            end\n            subgraph SDB[\"snet-db\"]");
        if (Has("azurerm_postgresql_flexible_server")) sb.AppendLine("                pg[(PostgreSQL Flexible Server 16<br/>acceso privado)]");
        if (Has("azurerm_virtual_network")) sb.AppendLine("            end\n        end");
        if (Has("azurerm_private_dns_zone")) sb.AppendLine("        dns[Private DNS Zone]");
        if (Has("azurerm_application_insights")) sb.AppendLine("        ai[Application Insights<br/>+ Log Analytics]");
        sb.AppendLine("    end");
        sb.AppendLine("    infra -->|aprovisiona| AZ");
        sb.AppendLine("    deploy -->|docker push| acr");
        sb.AppendLine("    deploy -->|build + upload| swa");
        sb.AppendLine("    acr -->|AcrPull| app");
        sb.AppendLine("    browser -->|HTTPS| swa");
        sb.AppendLine("    browser -->|HTTPS REST + JWT| app");
        sb.AppendLine("    app -->|TLS 5432| pg");
        sb.AppendLine("    app -.-> dns");
        sb.AppendLine("    app -.telemetría.-> ai");
        sb.AppendLine("```");
        return sb.ToString();
    }

    private static string Index() => Header("Documentación técnica de MusicStream") + """
        | Documento | Contenido |
        |---|---|
        | [Diccionario de datos](database/data-dictionary.md) | Tablas, columnas, tipos, claves e índices |
        | [Diagrama entidad-relación](database/er-diagram.md) | Modelo relacional (Mermaid `erDiagram`) |
        | [Diagrama de clases](architecture/class-diagram.md) | Dominio, servicios y controladores (Mermaid `classDiagram`) |
        | [Diagrama de componentes](architecture/component-diagram.md) | Frontend, API, servicios y datos (Mermaid `flowchart`) |
        | [Diagrama de despliegue](architecture/deployment-diagram.md) | Infraestructura Azure y pipelines (Mermaid `flowchart`) |
        """;
}
