# SchemaExtract

A lightweight .NET tool that extracts and documents SQL Server database schema structures to help AI agents understand database layouts.

## Purpose

SchemaExtract connects to a SQL Server database and generates detailed documentation for database objects, including:

**Tables:**
- Column definitions (name, type, nullability, identity, defaults, computed columns)
- Primary keys
- Unique constraints
- Foreign key relationships
- Check constraints

**Views:**
- Complete view definitions with SQL code

**Stored Procedures and Functions:**
- Complete procedure/function definitions with SQL code
- Supports scalar functions, table-valued functions, and stored procedures

This structured output enables AI agents to quickly understand database schemas without querying the database directly.

## Requirements

- .NET 9.0 or later
- Access to a SQL Server database

## Configuration

1. Copy `appsettings.example.json` to `appsettings.json`
2. Update the configuration with your database connection details

**Note:** `appsettings.json` is excluded from version control to protect sensitive credentials.

```json
{
  "OutputFolder": "C:\\path\\to\\output",
  "ConnectionString": "Data Source=server;User ID=username;Password=password;Initial Catalog=database;TrustServerCertificate=True"
}
```

### Configuration Properties

- **OutputFolder**: Directory where schema documentation will be saved
- **ConnectionString**: SQL Server connection string with appropriate credentials

## Usage

1. Configure `appsettings.json` with your database connection details
2. Run the application:
   ```bash
   dotnet run
   ```
3. Schema documentation will be generated in the configured output folder

## Output Structure

The tool creates organized folders for each database object type with markdown documentation.

Example output structure:
```
Results/
├── Tables/
│   ├── [dbo].[Users]/
│   │   └── dbo.Users.md
│   └── [dbo].[Orders]/
│       └── dbo.Orders.md
├── Views/
│   ├── [dbo].[vwActiveUsers]/
│   │   └── dbo.vwActiveUsers.md
│   └── [dbo].[vwOrderSummary]/
│       └── dbo.vwOrderSummary.md
├── Procedures/
│   └── [dbo].[sp_GetUserOrders]/
│       └── dbo.sp_GetUserOrders.md
└── Functions/
    └── [dbo].[fn_CalculateTotal]/
        └── dbo.fn_CalculateTotal.md
```

## Sample Output

### Table Documentation

Each table documentation file includes:

```markdown
# dbo.Users

**Generated:** 2025-11-06 14:20:00 +00:00

## Columns

| # | Column Name | Data Type | Nullable | Identity | Default | Computed |
|---|-------------|-----------|----------|----------|---------|----------|
| 1 | `UserId` | `int` | | ✓ | | |
| 2 | `Username` | `nvarchar(50)` | | | | |
| 3 | `Email` | `nvarchar(255)` | | | | |
| 4 | `CreatedDate` | `datetime2` | | | (getutcdate()) | |

## Primary Key

- **PK_Users**: `UserId`

## Unique Constraints

- **UQ_Users_Email**: `Email`

## Foreign Keys

*None*

## Check Constraints

- **CK_Users_Email**: `[Email] LIKE '%@%'`
```

### View Documentation

Views include their complete SQL definition:

```markdown
# dbo.vwActiveUsers

**Type:** View

**Generated:** 2025-11-06 14:20:00 +00:00

## Definition

\```sql
CREATE VIEW [dbo].[vwActiveUsers]
AS
SELECT u.UserId, u.Username, u.Email
FROM Users u
WHERE u.IsActive = 1
\```
```

### Stored Procedure/Function Documentation

Procedures and functions include their complete SQL definition with parameter information:

```markdown
# dbo.sp_GetUserOrders

**Type:** Stored Procedure

**Generated:** 2025-11-06 14:20:00 +00:00

## Definition

\```sql
CREATE PROCEDURE [dbo].[sp_GetUserOrders]
    @UserId INT
AS
BEGIN
    SELECT * FROM Orders WHERE UserId = @UserId
END
\```
```

## Use Cases

- AI-assisted database development and querying
- Database documentation generation
- Schema analysis and comparison
- Onboarding new developers to existing databases
- Database migration planning

## License

[Specify your license here]
