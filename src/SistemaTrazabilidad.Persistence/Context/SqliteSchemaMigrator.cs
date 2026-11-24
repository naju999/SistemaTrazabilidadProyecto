using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace SistemaTrazabilidad.Persistence.Context;

public static class SqliteSchemaMigrator
{
    private const string RelationshipMigrationSql = """
        CREATE TABLE "Lotes_new" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_Lotes" PRIMARY KEY AUTOINCREMENT,
            "CodigoLote" TEXT NOT NULL,
            "InsumoId" INTEGER NULL,
            "FechaIngreso" TEXT NOT NULL,
            "FechaVencimiento" TEXT NOT NULL,
            "CantidadInicial" TEXT NOT NULL,
            "CantidadDisponible" TEXT NOT NULL,
            "Bloqueado" INTEGER NOT NULL,
            "PEPS" INTEGER NOT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NULL,
            "IsActive" INTEGER NOT NULL,
            CONSTRAINT "CHK_Lote_CantidadDisponible" CHECK (CantidadDisponible >= 0),
            CONSTRAINT "FK_Lotes_Insumos_InsumoId" FOREIGN KEY ("InsumoId") REFERENCES "Insumos" ("Id") ON DELETE SET NULL
        );
        INSERT INTO "Lotes_new" SELECT * FROM "Lotes";
        DROP TABLE "Lotes";
        ALTER TABLE "Lotes_new" RENAME TO "Lotes";

        CREATE TABLE "Compras_new" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_Compras" PRIMARY KEY AUTOINCREMENT,
            "ProveedorId" INTEGER NOT NULL,
            "InsumoId" INTEGER NULL,
            "FechaCompra" TEXT NOT NULL,
            "Cantidad" TEXT NOT NULL,
            "PrecioUnitario" TEXT NOT NULL,
            "FechaEntregaEstimada" TEXT NOT NULL,
            "Estado" INTEGER NOT NULL,
            "Observaciones" TEXT NOT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NULL,
            "IsActive" INTEGER NOT NULL,
            CONSTRAINT "CHK_Compra_Cantidad" CHECK (Cantidad > 0),
            CONSTRAINT "CHK_Compra_PrecioUnitario" CHECK (PrecioUnitario >= 0),
            CONSTRAINT "FK_Compras_Insumos_InsumoId" FOREIGN KEY ("InsumoId") REFERENCES "Insumos" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_Compras_Proveedores_ProveedorId" FOREIGN KEY ("ProveedorId") REFERENCES "Proveedores" ("Id") ON DELETE RESTRICT
        );
        INSERT INTO "Compras_new" SELECT * FROM "Compras";
        DROP TABLE "Compras";
        ALTER TABLE "Compras_new" RENAME TO "Compras";

        CREATE TABLE "Producciones_new" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_Producciones" PRIMARY KEY AUTOINCREMENT,
            "OrdenProduccion" TEXT NOT NULL,
            "InsumoId" INTEGER NULL,
            "LoteId" INTEGER NULL,
            "FechaInicio" TEXT NOT NULL,
            "FechaFin" TEXT NULL,
            "CantidadProgramada" TEXT NOT NULL,
            "CantidadConsumida" TEXT NOT NULL,
            "Estado" INTEGER NOT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NULL,
            "IsActive" INTEGER NOT NULL,
            CONSTRAINT "CHK_Produccion_CantidadProgramada" CHECK (CantidadProgramada > 0),
            CONSTRAINT "FK_Producciones_Insumos_InsumoId" FOREIGN KEY ("InsumoId") REFERENCES "Insumos" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_Producciones_Lotes_LoteId" FOREIGN KEY ("LoteId") REFERENCES "Lotes" ("Id") ON DELETE SET NULL
        );
        INSERT INTO "Producciones_new" SELECT * FROM "Producciones";
        DROP TABLE "Producciones";
        ALTER TABLE "Producciones_new" RENAME TO "Producciones";

        CREATE TABLE "MovimientosInventario_new" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_MovimientosInventario" PRIMARY KEY AUTOINCREMENT,
            "InsumoId" INTEGER NULL,
            "LoteId" INTEGER NULL,
            "Tipo" INTEGER NOT NULL,
            "Cantidad" TEXT NOT NULL,
            "StockAnterior" TEXT NOT NULL,
            "StockPosterior" TEXT NOT NULL,
            "FechaMovimiento" TEXT NOT NULL,
            "UsuarioId" INTEGER NULL,
            "Referencia" TEXT NOT NULL,
            "Comentario" TEXT NOT NULL,
            "ProduccionId" INTEGER NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NULL,
            "IsActive" INTEGER NOT NULL,
            CONSTRAINT "CHK_Movimiento_Cantidad" CHECK (Cantidad > 0),
            CONSTRAINT "FK_MovimientosInventario_Insumos_InsumoId" FOREIGN KEY ("InsumoId") REFERENCES "Insumos" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_MovimientosInventario_Lotes_LoteId" FOREIGN KEY ("LoteId") REFERENCES "Lotes" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_MovimientosInventario_Producciones_ProduccionId" FOREIGN KEY ("ProduccionId") REFERENCES "Producciones" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_MovimientosInventario_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("Id")
        );
        INSERT INTO "MovimientosInventario_new" SELECT * FROM "MovimientosInventario";
        DROP TABLE "MovimientosInventario";
        ALTER TABLE "MovimientosInventario_new" RENAME TO "MovimientosInventario";

        CREATE TABLE "RegistrosSanitarios_new" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_RegistrosSanitarios" PRIMARY KEY AUTOINCREMENT,
            "InsumoId" INTEGER NULL,
            "FechaRegistro" TEXT NOT NULL,
            "Temperatura" TEXT NOT NULL,
            "Cloro" TEXT NOT NULL,
            "PH" TEXT NOT NULL,
            "Madurez" TEXT NOT NULL,
            "Observaciones" TEXT NOT NULL,
            "Aprobado" INTEGER NOT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NULL,
            "IsActive" INTEGER NOT NULL,
            CONSTRAINT "FK_RegistrosSanitarios_Insumos_InsumoId" FOREIGN KEY ("InsumoId") REFERENCES "Insumos" ("Id") ON DELETE SET NULL
        );
        INSERT INTO "RegistrosSanitarios_new" SELECT * FROM "RegistrosSanitarios";
        DROP TABLE "RegistrosSanitarios";
        ALTER TABLE "RegistrosSanitarios_new" RENAME TO "RegistrosSanitarios";

        CREATE INDEX "IX_Lotes_InsumoId" ON "Lotes" ("InsumoId");
        CREATE UNIQUE INDEX "IX_Lotes_CodigoLote" ON "Lotes" ("CodigoLote");
        CREATE INDEX "IX_Compras_InsumoId" ON "Compras" ("InsumoId");
        CREATE INDEX "IX_Compras_ProveedorId" ON "Compras" ("ProveedorId");
        CREATE INDEX "IX_Producciones_InsumoId" ON "Producciones" ("InsumoId");
        CREATE INDEX "IX_Producciones_LoteId" ON "Producciones" ("LoteId");
        CREATE INDEX "IX_MovimientosInventario_InsumoId" ON "MovimientosInventario" ("InsumoId");
        CREATE INDEX "IX_MovimientosInventario_LoteId" ON "MovimientosInventario" ("LoteId");
        CREATE INDEX "IX_MovimientosInventario_ProduccionId" ON "MovimientosInventario" ("ProduccionId");
        CREATE INDEX "IX_MovimientosInventario_UsuarioId" ON "MovimientosInventario" ("UsuarioId");
        CREATE INDEX "IX_RegistrosSanitarios_InsumoId" ON "RegistrosSanitarios" ("InsumoId");
        """;

    public static async Task ApplyAsync(AppDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection)
        {
            await connection.OpenAsync();
        }

        try
        {
            if (await RequiresRelationshipMigrationAsync(connection))
            {
                await ExecuteAsync(connection, null, "PRAGMA foreign_keys = OFF;");
                await using var transaction = await connection.BeginTransactionAsync();
                await ExecuteAsync(connection, transaction, RelationshipMigrationSql);
                await ValidateForeignKeysAsync(connection, transaction);
                await transaction.CommitAsync();
            }

            await EnsureLotProvenanceAsync(connection);
            await RemoveSupplierDeliveryDaysAsync(connection);
        }
        finally
        {
            await ExecuteAsync(connection, null, "PRAGMA foreign_keys = ON;");
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> RequiresRelationshipMigrationAsync(DbConnection connection)
    {
        return await IsNotNullAsync(connection, "Lotes", "InsumoId")
            || await IsNotNullAsync(connection, "Compras", "InsumoId")
            || await IsNotNullAsync(connection, "Producciones", "InsumoId")
            || await IsNotNullAsync(connection, "Producciones", "LoteId")
            || await IsNotNullAsync(connection, "MovimientosInventario", "InsumoId")
            || await IsNotNullAsync(connection, "RegistrosSanitarios", "InsumoId");
    }

    private static async Task<bool> IsNotNullAsync(DbConnection connection, string table, string column)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.Ordinal))
            {
                return reader.GetInt64(3) != 0;
            }
        }

        throw new InvalidOperationException($"No se encontró la columna {table}.{column} durante la actualización de la base de datos.");
    }

    private static async Task EnsureLotProvenanceAsync(DbConnection connection)
    {
        await using var transaction = await connection.BeginTransactionAsync();

        if (!await HasColumnAsync(connection, transaction, "Lotes", "CodigoInterno"))
        {
            await ExecuteAsync(connection, transaction,
                "ALTER TABLE \"Lotes\" ADD COLUMN \"CodigoInterno\" TEXT NOT NULL DEFAULT '';");
        }

        if (!await HasColumnAsync(connection, transaction, "Lotes", "ProveedorId"))
        {
            await ExecuteAsync(connection, transaction,
                "ALTER TABLE \"Lotes\" ADD COLUMN \"ProveedorId\" INTEGER NULL REFERENCES \"Proveedores\" (\"Id\") ON DELETE RESTRICT;");
        }

        await ExecuteAsync(connection, transaction, """
            UPDATE "Lotes"
            SET "CodigoInterno" = 'LOT-' || COALESCE(strftime('%Y%m%d', "FechaIngreso"), '00000000')
                || '-' || printf('%016X', "Id")
            WHERE "CodigoInterno" IS NULL OR trim("CodigoInterno") = '';
            DROP INDEX IF EXISTS "IX_Lotes_CodigoLote";
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Lotes_CodigoInterno" ON "Lotes" ("CodigoInterno");
            CREATE INDEX IF NOT EXISTS "IX_Lotes_ProveedorId" ON "Lotes" ("ProveedorId");
            """);

        await ValidateForeignKeysAsync(connection, transaction);
        await transaction.CommitAsync();
    }

    private static async Task<bool> HasColumnAsync(
        DbConnection connection,
        DbTransaction transaction,
        string table,
        string column)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task RemoveSupplierDeliveryDaysAsync(DbConnection connection)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        if (await HasColumnAsync(connection, transaction, "Proveedores", "TiempoEntregaDias"))
        {
            await ExecuteAsync(
                connection,
                transaction,
                "ALTER TABLE \"Proveedores\" DROP COLUMN \"TiempoEntregaDias\";");
        }

        await transaction.CommitAsync();
    }

    private static async Task ExecuteAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ValidateForeignKeysAsync(DbConnection connection, DbTransaction transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA foreign_key_check;";
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            throw new InvalidOperationException(
                $"La actualización dejó una referencia inválida en la tabla {reader.GetString(0)}.");
        }
    }
}
