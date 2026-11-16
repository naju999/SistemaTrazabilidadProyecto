# INSTRUCCIONES DE INSTALACIÓN Y EJECUCIÓN

## 1. REQUISITOS PREVIOS

- .NET 8.0 SDK instalado
- Visual Studio 2022, VS Code o JetBrains Rider
- SQLite (incluido con .NET)

## 2. PASOS DE CONFIGURACIÓN

### 2.1 Restaurar Dependencias NuGet
```bash
cd d:\Universidad\Software\SistemaTrazabilidadProyecto
dotnet restore SistemaTrazabilidad.sln
```

### 2.2 Compilar la Solución
```bash
dotnet build SistemaTrazabilidad.sln
```

### 2.3 Generar Migrations de EF Core

Las migraciones se generan automáticamente la primera vez que se ejecuta la aplicación.
El `SeedDataInitializer` en `App.xaml.cs` se encarga de crear la base de datos y llenarla.

Si necesitas hacerlo manualmente:

```bash
# Ir a la carpeta Persistence
cd src/SistemaTrazabilidad.Persistence

# Crear la migración
dotnet ef migrations add InitialCreate -p SistemaTrazabilidad.Persistence.csproj -s ..\SistemaTrazabilidad.Presentation\SistemaTrazabilidad.Presentation.csproj

# Actualizar la base de datos
dotnet ef database update -p SistemaTrazabilidad.Persistence.csproj -s ..\SistemaTrazabilidad.Presentation\SistemaTrazabilidad.Presentation.csproj
```

### 2.4 Ejecutar la Aplicación

```bash
cd src/SistemaTrazabilidad.Presentation
dotnet run
```

O desde Visual Studio:
- Click derecho en `SistemaTrazabilidad.Presentation` → Set as Startup Project
- F5 para ejecutar

## 3. CREDENCIALES DE ACCESO INICIAL

**Usuario:** admin
**Contraseña:** admin123

## 4. ESTRUCTURA DE MIGRACIONES

Las migraciones se encuentran en: `src/SistemaTrazabilidad.Persistence/Migrations/`

Cada migración contiene:
- Creación de tablas con tipos SQLite
- Índices únicos
- Check constraints
- Relaciones y foreign keys

## 5. UBICACIÓN DE LA BASE DE DATOS

```
d:\Universidad\Software\SistemaTrazabilidadProyecto\traceability.db
```

Para acceder a la BD con SQLite:
```bash
sqlite3 traceability.db
```

## 6. UBICACIÓN DE LOGS

```
d:\Universidad\Software\SistemaTrazabilidadProyecto\logs\
```

Los logs se generan diarios con nombres: `app-YYYY-MM-DD.txt`

## 7. DESCRIPCIÓN DE ENTIDADES Y RELACIONES

### Usuario
- Username (único)
- NombreCompleto
- PasswordHash
- RolId (FK a Rol)
- Activo

### Rol
- Nombre (único)
- Descripción

### Permiso
- Codigo (único)
- Nombre
- Descripcion

### Proveedor
- Codigo (único)
- Nombre
- Contacto
- Email
- TiempoEntregaDias
- Activo

### Insumo
- Codigo (único)
- Nombre
- UnidadMedida
- StockActual (>= 0)
- StockMinimo (>= 0)
- Activo

### Lote
- CodigoLote (único)
- InsumoId (FK)
- FechaIngreso
- FechaVencimiento
- CantidadDisponible (>= 0)
- Bloqueado

### Compra
- ProveedorId (FK)
- InsumoId (FK)
- FechaCompra
- Cantidad (> 0)
- PrecioUnitario (>= 0)
- Estado (Pendiente, Recibida, Cancelada)

### Produccion
- OrdenProduccion (unique)
- InsumoId (FK)
- LoteId (FK)
- FechaInicio
- CantidadProgramada (> 0)
- Estado (Iniciada, EnProceso, Completada, Cancelada)

### MovimientoInventario
- InsumoId (FK)
- LoteId (FK nullable)
- Cantidad (> 0)
- Tipo (Entrada, Salida, Ajuste)
- FechaMovimiento

### RegistroSanitario
- InsumoId (FK)
- FechaRegistro
- Temperatura
- Cloro
- PH
- Madurez
- Aprobado

### Auditoria
- Entidad (nombre de la tabla)
- EntidadId
- UsuarioId (FK)
- Operacion (Create, Update, Delete)
- FechaHora (datetime default)

### Alerta
- Codigo (único)
- Mensaje
- Tipo (StockBajo, VencimientoPróximo, ControlDefectuoso, etc)
- FechaGeneracion
- Activa
- Leida

## 8. TROUBLESHOOTING

### Error: "No .NET SDKs were found"
→ Instala .NET 8.0 SDK desde https://dotnet.microsoft.com/download

### Error: "No such table: Usuarios"
→ Ejecuta manualmente la migración:
```bash
dotnet ef database update
```

### Error: "Connection string not found"
→ Verifica que `appsettings.json` exista en la raíz del proyecto

### Error: "PasswordHasher not found"
→ Asegúrate que `SistemaTrazabilidad.Infrastructure` está referenciado en los proyectos necesarios

## 9. PRÓXIMOS PASOS

1. ✅ Backend configurado y bases de datos lista
2. ✅ Autenticación funcional
3. ✅ Shell de la aplicación con navegación
4. ⏳ Implementar vistas de cada módulo (Inventory, Purchases, Production, etc)
5. ⏳ Integrar servicios con las vistas
6. ⏳ Reportes y exportación
7. ⏳ Testing completo

---

**Versión:** 1.0.0 (Fase 1 - Base Operativa)
**Última actualización:** 01-06-2026
