# SchemaExtract

A lightweight .NET tool that extracts and documents SQL Server database schema structures to help AI agents understand database layouts.

## Purpose

SchemaExtract connects to a SQL Server database and generates detailed documentation for each table, including:
- Column definitions (name, type, nullability, identity, defaults, computed columns)
- Primary keys
- Unique constraints
- Foreign key relationships
- Check constraints

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

The tool creates a folder for each table with the naming pattern `[SchemaName].[TableName]` containing a markdown file with complete schema details.

Example output structure:
```
Results/
├── [dbo].[Users]/
│   └── dbo.Users.md
├── [dbo].[Orders]/
│   └── dbo.Orders.md
└── ...
```

## Sample Output

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

## Use Cases

- AI-assisted database development and querying
- Database documentation generation
- Schema analysis and comparison
- Onboarding new developers to existing databases
- Database migration planning

## License

[Specify your license here]
