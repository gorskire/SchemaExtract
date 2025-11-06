using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;

static class Program
{
    private record Config(string OutputFolder, string ConnectionString);
    private record TableRef(int ObjectId, string SchemaName, string TableName);
    private record ViewRef(int ObjectId, string SchemaName, string ViewName, string Definition);
    private record RoutineRef(int ObjectId, string SchemaName, string RoutineName, string RoutineType, string Definition);
    private record ColumnInfo(
        int ColumnId,
        string ColumnName,
        string TypeName,
        short MaxLength,
        byte Precision,
        byte Scale,
        bool IsNullable,
        bool IsIdentity,
        string? DefaultDefinition,
        bool IsComputed,
        string? ComputedDefinition
    );
    private record KeyPart(string ConstraintName, byte KeyOrdinal, string ColumnName);
    private record ForeignKeyPart(
        string ConstraintName,
        string ParentSchema,
        string ParentTable,
        string ParentColumn,
        string RefSchema,
        string RefTable,
        string RefColumn,
        int Ordinal
    );
    private record CheckConstraintInfo(string ConstraintName, string? ColumnName, string Definition);

    public static async Task Main()
    {
        Console.WriteLine("Current Directory: " + Directory.GetCurrentDirectory());
        Console.WriteLine(File.Exists("appsettings.json") ? "Found appsettings.json" : "Missing appsettings.json");

        var config = LoadConfig();
        if (config is null)
        {
            Console.WriteLine("Missing or invalid appsettings.json. Please create one with OutputFolder and ConnectionString.");
            return;
        }

        Directory.CreateDirectory(config.OutputFolder);

        // Clean output folder before extraction
        if (Directory.Exists(config.OutputFolder))
        {
            foreach (var dir in Directory.GetDirectories(config.OutputFolder))
                Directory.Delete(dir, true);
            foreach (var file in Directory.GetFiles(config.OutputFolder))
                File.Delete(file);
        }

        using var conn = new SqlConnection(config.ConnectionString);
        await conn.OpenAsync();

        var tables = await GetTables(conn);
        foreach (var t in tables)
        {
            var dirName = $"[{t.SchemaName}].[{t.TableName}]";
            var tableDir = Path.Combine(config.OutputFolder, "Tables", dirName);
            Directory.CreateDirectory(tableDir);

            var details = await BuildTableReport(conn, t);
            var fileName = Path.Combine(tableDir, $"{t.SchemaName}.{t.TableName}.md");
            await File.WriteAllTextAsync(fileName, details, new UTF8Encoding(false));
            Console.WriteLine($"Wrote {fileName}");
        }

        var views = await GetViews(conn);
        foreach (var v in views)
        {
            var dirName = $"[{v.SchemaName}].[{v.ViewName}]";
            var viewDir = Path.Combine(config.OutputFolder, "Views", dirName);
            Directory.CreateDirectory(viewDir);

            var details = BuildViewReport(v);
            var fileName = Path.Combine(viewDir, $"{v.SchemaName}.{v.ViewName}.md");
            await File.WriteAllTextAsync(fileName, details, new UTF8Encoding(false));
            Console.WriteLine($"Wrote {fileName}");
        }

        var routines = await GetRoutines(conn);
        foreach (var r in routines)
        {
            var dirName = $"[{r.SchemaName}].[{r.RoutineName}]";
            var routineDir = Path.Combine(config.OutputFolder, r.RoutineType == "SQL_STORED_PROCEDURE" ? "Procedures" : "Functions", dirName);
            Directory.CreateDirectory(routineDir);

            var details = BuildRoutineReport(r);
            var fileName = Path.Combine(routineDir, $"{r.SchemaName}.{r.RoutineName}.md");
            await File.WriteAllTextAsync(fileName, details, new UTF8Encoding(false));
            Console.WriteLine($"Wrote {fileName}");
        }

        // Generate summary file
        await GenerateSummary(config.OutputFolder, tables, views, routines);

        Console.WriteLine("Done.");
    }

    private static async Task GenerateSummary(string outputFolder, List<TableRef> tables, List<ViewRef> views, List<RoutineRef> routines)
    {
        var sb = new StringBuilder();
        var now = DateTimeOffset.Now;

        sb.AppendLine("# Database Schema Summary");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine($"- **Tables:** {tables.Count}");
        sb.AppendLine($"- **Views:** {views.Count}");
        sb.AppendLine($"- **Stored Procedures:** {routines.Count(r => r.RoutineType == "SQL_STORED_PROCEDURE")}");
        sb.AppendLine($"- **Functions:** {routines.Count(r => r.RoutineType != "SQL_STORED_PROCEDURE")}");
        sb.AppendLine();

        // Tables
        sb.AppendLine("## Tables");
        sb.AppendLine();
        foreach (var schemaGroup in tables.GroupBy(t => t.SchemaName).OrderBy(g => g.Key))
        {
            sb.AppendLine($"### {schemaGroup.Key}");
            sb.AppendLine();
            foreach (var table in schemaGroup.OrderBy(t => t.TableName))
            {
                sb.AppendLine($"- [{table.TableName}](Tables/[{table.SchemaName}].[{table.TableName}]/{table.SchemaName}.{table.TableName}.md)");
            }
            sb.AppendLine();
        }

        // Views
        if (views.Count > 0)
        {
            sb.AppendLine("## Views");
            sb.AppendLine();
            foreach (var schemaGroup in views.GroupBy(v => v.SchemaName).OrderBy(g => g.Key))
            {
                sb.AppendLine($"### {schemaGroup.Key}");
                sb.AppendLine();
                foreach (var view in schemaGroup.OrderBy(v => v.ViewName))
                {
                    sb.AppendLine($"- [{view.ViewName}](Views/[{view.SchemaName}].[{view.ViewName}]/{view.SchemaName}.{view.ViewName}.md)");
                }
                sb.AppendLine();
            }
        }

        // Stored Procedures
        var procedures = routines.Where(r => r.RoutineType == "SQL_STORED_PROCEDURE").ToList();
        if (procedures.Count > 0)
        {
            sb.AppendLine("## Stored Procedures");
            sb.AppendLine();
            foreach (var schemaGroup in procedures.GroupBy(p => p.SchemaName).OrderBy(g => g.Key))
            {
                sb.AppendLine($"### {schemaGroup.Key}");
                sb.AppendLine();
                foreach (var proc in schemaGroup.OrderBy(p => p.RoutineName))
                {
                    sb.AppendLine($"- [{proc.RoutineName}](Procedures/[{proc.SchemaName}].[{proc.RoutineName}]/{proc.SchemaName}.{proc.RoutineName}.md)");
                }
                sb.AppendLine();
            }
        }

        // Functions
        var functions = routines.Where(r => r.RoutineType != "SQL_STORED_PROCEDURE").ToList();
        if (functions.Count > 0)
        {
            sb.AppendLine("## Functions");
            sb.AppendLine();
            foreach (var schemaGroup in functions.GroupBy(f => f.SchemaName).OrderBy(g => g.Key))
            {
                sb.AppendLine($"### {schemaGroup.Key}");
                sb.AppendLine();
                foreach (var func in schemaGroup.OrderBy(f => f.RoutineName))
                {
                    var typeName = func.RoutineType switch
                    {
                        "SQL_SCALAR_FUNCTION" => "Scalar Function",
                        "SQL_INLINE_TABLE_VALUED_FUNCTION" => "Inline Table-Valued Function",
                        "SQL_TABLE_VALUED_FUNCTION" => "Table-Valued Function",
                        _ => func.RoutineType
                    };
                    sb.AppendLine($"- [{func.RoutineName}](Functions/[{func.SchemaName}].[{func.RoutineName}]/{func.SchemaName}.{func.RoutineName}.md) - *{typeName}*");
                }
                sb.AppendLine();
            }
        }

        var summaryPath = Path.Combine(outputFolder, "summary.md");
        await File.WriteAllTextAsync(summaryPath, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"Wrote {summaryPath}");
    }

    private static Config? LoadConfig()
    {
        const string file = "appsettings.json";
        if (!File.Exists(file)) return null;

        try
        {
            var json = File.ReadAllText(file);
            return JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading config: {ex.Message}");
            return null;
        }
    }

    private static async Task<List<TableRef>> GetTables(SqlConnection conn)
    {
        const string sql = @"SELECT t.object_id, s.name AS SchemaName, t.name AS TableName
                             FROM sys.tables t
                             JOIN sys.schemas s ON s.schema_id = t.schema_id
                             WHERE t.is_ms_shipped = 0
                             ORDER BY s.name, t.name;";
        using var cmd = new SqlCommand(sql, conn);
        using var rdr = await cmd.ExecuteReaderAsync();
        var list = new List<TableRef>();
        while (await rdr.ReadAsync())
            list.Add(new TableRef(rdr.GetInt32(0), rdr.GetString(1), rdr.GetString(2)));
        return list;
    }

    private static async Task<List<ViewRef>> GetViews(SqlConnection conn)
    {
        const string sql = @"
SELECT 
    v.object_id, 
    s.name AS SchemaName, 
    v.name AS ViewName,
    m.definition
FROM sys.views v
JOIN sys.schemas s ON s.schema_id = v.schema_id
JOIN sys.sql_modules m ON m.object_id = v.object_id
WHERE v.is_ms_shipped = 0
ORDER BY s.name, v.name;";
        
        using var cmd = new SqlCommand(sql, conn);
        using var rdr = await cmd.ExecuteReaderAsync();
        var list = new List<ViewRef>();
        while (await rdr.ReadAsync())
            list.Add(new ViewRef(
                rdr.GetInt32(0), 
                rdr.GetString(1), 
                rdr.GetString(2),
                rdr.GetString(3)));
        return list;
    }

    private static async Task<List<RoutineRef>> GetRoutines(SqlConnection conn)
    {
        const string sql = @"
SELECT 
    r.object_id,
    s.name AS SchemaName,
    r.name AS RoutineName,
    r.type_desc AS RoutineType,
    m.definition
FROM sys.objects r
JOIN sys.schemas s ON s.schema_id = r.schema_id
JOIN sys.sql_modules m ON m.object_id = r.object_id
WHERE r.type IN ('P', 'FN', 'IF', 'TF')
  AND r.is_ms_shipped = 0
ORDER BY r.type_desc, s.name, r.name;";
        
        using var cmd = new SqlCommand(sql, conn);
        using var rdr = await cmd.ExecuteReaderAsync();
        var list = new List<RoutineRef>();
        while (await rdr.ReadAsync())
            list.Add(new RoutineRef(
                rdr.GetInt32(0),
                rdr.GetString(1),
                rdr.GetString(2),
                rdr.GetString(3),
                rdr.GetString(4)));
        return list;
    }

    private static async Task<string> BuildTableReport(SqlConnection conn, TableRef t)
    {
        var sb = new StringBuilder();
        var now = DateTimeOffset.Now;

        sb.AppendLine($"# {t.SchemaName}.{t.TableName}");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();

        var columns = await GetColumns(conn, t.ObjectId);
        sb.AppendLine("## Columns");
        sb.AppendLine();
        sb.AppendLine("| # | Column Name | Data Type | Nullable | Identity | Default | Computed |");
        sb.AppendLine("|---|-------------|-----------|----------|----------|---------|----------|");
        
        foreach (var c in columns.OrderBy(x => x.ColumnId))
        {
            var typeStr = RenderType(c);
            var nullStr = c.IsNullable ? "✓" : "";
            var identityStr = c.IsIdentity ? "✓" : "";
            var computedStr = c.IsComputed ? c.ComputedDefinition ?? "" : "";
            var defaultStr = c.DefaultDefinition ?? "";
            
            sb.AppendLine($"| {c.ColumnId} | `{c.ColumnName}` | `{typeStr}` | {nullStr} | {identityStr} | {EscapeMarkdown(defaultStr)} | {EscapeMarkdown(computedStr)} |");
        }

        sb.AppendLine();
        sb.Append(await RenderKeysAndConstraints(conn, t));
        return sb.ToString();
    }

    private static string EscapeMarkdown(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        return text.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
    }

    private static async Task<string> RenderKeysAndConstraints(SqlConnection conn, TableRef t)
    {
        var sb = new StringBuilder();

        var pk = await GetPrimaryKey(conn, t.ObjectId);
        sb.AppendLine("## Primary Key");
        sb.AppendLine();
        if (pk.Count == 0)
        {
            sb.AppendLine("*None*");
        }
        else
        {
            foreach (var g in pk.GroupBy(x => x.ConstraintName))
            {
                var cols = string.Join(", ", g.OrderBy(x => x.KeyOrdinal).Select(x => $"`{x.ColumnName}`"));
                sb.AppendLine($"- **{g.Key}**: {cols}");
            }
        }

        var uqs = await GetUniqueConstraints(conn, t.ObjectId);
        sb.AppendLine();
        sb.AppendLine("## Unique Constraints");
        sb.AppendLine();
        if (uqs.Count == 0)
        {
            sb.AppendLine("*None*");
        }
        else
        {
            foreach (var g in uqs.GroupBy(x => x.ConstraintName))
            {
                var cols = string.Join(", ", g.OrderBy(x => x.KeyOrdinal).Select(x => $"`{x.ColumnName}`"));
                sb.AppendLine($"- **{g.Key}**: {cols}");
            }
        }

        var fks = await GetForeignKeys(conn, t.ObjectId);
        sb.AppendLine();
        sb.AppendLine("## Foreign Keys");
        sb.AppendLine();
        if (fks.Count == 0)
        {
            sb.AppendLine("*None*");
        }
        else
        {
            foreach (var g in fks.GroupBy(x => x.ConstraintName))
            {
                var ordered = g.OrderBy(x => x.Ordinal).ToList();
                var srcCols = string.Join(", ", ordered.Select(x => $"`{x.ParentColumn}`"));
                var refTable = $"`{ordered[0].RefSchema}.{ordered[0].RefTable}`";
                var refCols = string.Join(", ", ordered.Select(x => $"`{x.RefColumn}`"));
                sb.AppendLine($"- **{g.Key}**: ({srcCols}) → {refTable} ({refCols})");
            }
        }

        var checks = await GetCheckConstraints(conn, t.ObjectId);
        sb.AppendLine();
        sb.AppendLine("## Check Constraints");
        sb.AppendLine();
        if (checks.Count == 0)
        {
            sb.AppendLine("*None*");
        }
        else
        {
            foreach (var c in checks.OrderBy(x => x.ConstraintName))
            {
                sb.AppendLine($"- **{c.ConstraintName}**: `{c.Definition}`");
            }
        }

        return sb.ToString();
    }

    private static async Task<List<ColumnInfo>> GetColumns(SqlConnection conn, int objectId)
    {
        const string sql = @"
SELECT 
    c.column_id,
    c.name AS ColumnName,
    ty.name AS TypeName,
    c.max_length,
    c.precision,
    c.scale,
    c.is_nullable,
    c.is_identity,
    dc.definition AS DefaultDefinition,
    c.is_computed,
    cc.definition AS ComputedDefinition
FROM sys.columns c
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
LEFT JOIN sys.computed_columns cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id
WHERE c.object_id = @obj
ORDER BY c.column_id;";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@obj", objectId);
        var list = new List<ColumnInfo>();
        using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
        {
            list.Add(new ColumnInfo(
                rdr.GetInt32(0),
                rdr.GetString(1),
                rdr.GetString(2),
                rdr.GetInt16(3),
                rdr.GetByte(4),
                rdr.GetByte(5),
                rdr.GetBoolean(6),
                rdr.GetBoolean(7),
                rdr.IsDBNull(8) ? null : rdr.GetString(8),
                rdr.GetBoolean(9),
                rdr.IsDBNull(10) ? null : rdr.GetString(10)
            ));
        }
        return list;
    }

    private static string RenderType(ColumnInfo c)
    {
        string t = c.TypeName.ToLowerInvariant();

        if (t is "decimal" or "numeric")
            return $"{t}({c.Precision},{c.Scale})";

        if (t is "datetime2" or "datetimeoffset" or "time")
            return c.Scale > 0 ? $"{t}({c.Scale})" : t;

        if (t is "float")
            return c.Precision is 53 or 0 ? "float" : $"float({c.Precision})";

        if (t is "varbinary" or "varchar" or "nvarchar")
        {
            int length = c.MaxLength;
            if (t.StartsWith("n")) length = length < 0 ? length : length / 2;
            return $"{t}({(length == -1 ? "max" : length.ToString(CultureInfo.InvariantCulture))})";
        }

        if (t is "binary" or "char" or "nchar")
        {
            int length = t.StartsWith("n") ? c.MaxLength / 2 : c.MaxLength;
            return $"{t}({length})";
        }

        return t;
    }

    private static async Task<List<KeyPart>> GetPrimaryKey(SqlConnection conn, int objectId)
    {
        const string sql = @"
SELECT kc.name, ic.key_ordinal, col.name
FROM sys.key_constraints kc
JOIN sys.index_columns ic 
  ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
JOIN sys.columns col
  ON col.object_id = ic.object_id AND col.column_id = ic.column_id
WHERE kc.parent_object_id = @obj AND kc.type = 'PK'
ORDER BY kc.name, ic.key_ordinal;";

        return await ReadKeyParts(conn, objectId, sql);
    }

    private static async Task<List<KeyPart>> GetUniqueConstraints(SqlConnection conn, int objectId)
    {
        const string sql = @"
SELECT kc.name, ic.key_ordinal, col.name
FROM sys.key_constraints kc
JOIN sys.index_columns ic 
  ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
JOIN sys.columns col
  ON col.object_id = ic.object_id AND col.column_id = ic.column_id
WHERE kc.parent_object_id = @obj AND kc.type = 'UQ'
ORDER BY kc.name, ic.key_ordinal;";
        return await ReadKeyParts(conn, objectId, sql);
    }

    private static async Task<List<KeyPart>> ReadKeyParts(SqlConnection conn, int objectId, string sql)
    {
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@obj", objectId);
        using var rdr = await cmd.ExecuteReaderAsync();
        var list = new List<KeyPart>();
        while (await rdr.ReadAsync())
        {
            list.Add(new KeyPart(
                rdr.GetString(0),
                rdr.GetByte(1),
                rdr.GetString(2)
            ));
        }
        return list;
    }

    private static async Task<List<ForeignKeyPart>> GetForeignKeys(SqlConnection conn, int objectId)
    {
        const string sql = @"
SELECT 
    fk.name,
    schP.name AS ParentSchema,
    tP.name   AS ParentTable,
    colP.name AS ParentColumn,
    schR.name AS RefSchema,
    tR.name   AS RefTable,
    colR.name AS RefColumn,
    fkc.constraint_column_id
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables tP   ON tP.object_id = fk.parent_object_id
JOIN sys.schemas schP ON schP.schema_id = tP.schema_id
JOIN sys.tables tR   ON tR.object_id = fk.referenced_object_id
JOIN sys.schemas schR ON schR.schema_id = tR.schema_id
JOIN sys.columns colP ON colP.object_id = fk.parent_object_id AND colP.column_id = fkc.parent_column_id
JOIN sys.columns colR ON colR.object_id = fk.referenced_object_id AND colR.column_id = fkc.referenced_column_id
WHERE fk.parent_object_id = @obj
ORDER BY fk.name, fkc.constraint_column_id;";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@obj", objectId);
        using var rdr = await cmd.ExecuteReaderAsync();
        var list = new List<ForeignKeyPart>();
        while (await rdr.ReadAsync())
        {
            list.Add(new ForeignKeyPart(
                rdr.GetString(0),
                rdr.GetString(1),
                rdr.GetString(2),
                rdr.GetString(3),
                rdr.GetString(4),
                rdr.GetString(5),
                rdr.GetString(6),
                rdr.GetInt32(7)
            ));
        }
        return list;
    }

    private static async Task<List<CheckConstraintInfo>> GetCheckConstraints(SqlConnection conn, int objectId)
    {
        const string sql = @"
SELECT 
    cc.name,
    col.name AS ColumnName,
    cc.definition
FROM sys.check_constraints cc
LEFT JOIN sys.columns col 
    ON col.object_id = cc.parent_object_id AND col.column_id = cc.parent_column_id
WHERE cc.parent_object_id = @obj
ORDER BY cc.name;";

        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@obj", objectId);
        using var rdr = await cmd.ExecuteReaderAsync();
        var list = new List<CheckConstraintInfo>();
        while (await rdr.ReadAsync())
        {
            list.Add(new CheckConstraintInfo(
                rdr.GetString(0),
                rdr.IsDBNull(1) ? null : rdr.GetString(1),
                rdr.GetString(2)
            ));
        }
        return list;
    }

    private static string BuildViewReport(ViewRef v)
    {
        var sb = new StringBuilder();
        var now = DateTimeOffset.Now;

        sb.AppendLine($"# {v.SchemaName}.{v.ViewName}");
        sb.AppendLine();
        sb.AppendLine($"**Type:** View");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();
        sb.AppendLine("## Definition");
        sb.AppendLine();
        sb.AppendLine("```sql");
        sb.AppendLine(v.Definition.Trim());
        sb.AppendLine("```");

        return sb.ToString();
    }

    private static string BuildRoutineReport(RoutineRef r)
    {
        var sb = new StringBuilder();
        var now = DateTimeOffset.Now;
        
        var typeName = r.RoutineType switch
        {
            "SQL_STORED_PROCEDURE" => "Stored Procedure",
            "SQL_SCALAR_FUNCTION" => "Scalar Function",
            "SQL_INLINE_TABLE_VALUED_FUNCTION" => "Inline Table-Valued Function",
            "SQL_TABLE_VALUED_FUNCTION" => "Table-Valued Function",
            _ => r.RoutineType
        };

        sb.AppendLine($"# {r.SchemaName}.{r.RoutineName}");
        sb.AppendLine();
        sb.AppendLine($"**Type:** {typeName}");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();
        sb.AppendLine("## Definition");
        sb.AppendLine();
        sb.AppendLine("```sql");
        sb.AppendLine(r.Definition.Trim());
        sb.AppendLine("```");

        return sb.ToString();
    }
}
