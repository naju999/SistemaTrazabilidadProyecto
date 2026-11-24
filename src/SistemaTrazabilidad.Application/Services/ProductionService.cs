using System.Globalization;
using SistemaTrazabilidad.Application.Exceptions;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Domain.Enums;
using SistemaTrazabilidad.Persistence.Repositories;

namespace SistemaTrazabilidad.Application.Services;

public sealed record ProductionOrderLine(int InsumoId, int LoteId, decimal CantidadProgramada);

public class ProductionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditService _auditService;
    private readonly InventoryService _inventoryService;

    public ProductionService(
        IUnitOfWork unitOfWork,
        AuditService auditService,
        InventoryService inventoryService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _inventoryService = inventoryService;
    }

    public async Task<IReadOnlyList<Produccion>> CreateOrdenProduccionAsync(
        DateTime fechaInicio,
        IReadOnlyList<ProductionOrderLine> lines,
        int usuarioId)
    {
        if (fechaInicio.Date < DateTime.Today)
        {
            throw new BusinessRuleException(
                "La fecha de inicio de producción debe ser hoy o una fecha futura.");
        }

        if (lines.Count == 0)
        {
            throw new BusinessRuleException("La orden debe incluir al menos un insumo.");
        }

        if (lines.Any(line => line.CantidadProgramada <= 0))
        {
            throw new BusinessRuleException("La cantidad programada debe ser mayor que cero.");
        }

        var inputs = new Dictionary<int, Insumo>();
        var lots = new Dictionary<int, Lote>();
        foreach (var line in lines)
        {
            if (!inputs.ContainsKey(line.InsumoId))
            {
                var input = await _unitOfWork.Repository<Insumo>().GetByIdAsync(line.InsumoId)
                    ?? throw new BusinessRuleException("Insumo no encontrado.");
                if (!input.Activo)
                {
                    throw new BusinessRuleException(
                        $"No se puede programar producción con el insumo {input.Nombre} inactivo.");
                }

                inputs.Add(line.InsumoId, input);
            }

            if (!lots.ContainsKey(line.LoteId))
            {
                var lot = await _unitOfWork.Repository<Lote>().GetByIdAsync(line.LoteId)
                    ?? throw new BusinessRuleException("Lote no encontrado.");
                if (lot.Bloqueado || lot.EstaVencido)
                {
                    throw new BusinessRuleException(
                        $"No se puede usar el lote {lot.CodigoInterno} porque está bloqueado o vencido.");
                }

                if (lot.InsumoId != line.InsumoId)
                {
                    throw new BusinessRuleException(
                        $"El lote {lot.CodigoInterno} no corresponde al insumo seleccionado.");
                }

                lots.Add(line.LoteId, lot);
            }
        }

        var requestedByLot = lines
            .GroupBy(line => line.LoteId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.CantidadProgramada));
        foreach (var request in requestedByLot)
        {
            var lot = lots[request.Key];
            if (request.Value > lot.CantidadDisponible)
            {
                throw new BusinessRuleException(
                    $"El lote {lot.CodigoInterno} no tiene disponibilidad suficiente. " +
                    $"Disponible: {lot.CantidadDisponible}; requerido: {request.Value}.");
            }
        }

        var availableByInput = new Dictionary<int, decimal>();
        foreach (var inputId in lines.Select(line => line.InsumoId).Distinct())
        {
            var available = await _inventoryService.GetAvailableQuantityAsync(inputId);
            var requested = lines
                .Where(line => line.InsumoId == inputId)
                .Sum(line => line.CantidadProgramada);
            if (available < requested)
            {
                throw new BusinessRuleException(
                    $"El stock disponible de {inputs[inputId].Nombre} no alcanza para reservar la producción. " +
                    $"Disponible: {available}; requerido: {requested}.");
            }

            availableByInput.Add(inputId, available);
        }

        var orderNumber = await GenerateOrderNumberAsync();
        var productions = new List<Produccion>(lines.Count);
        foreach (var line in lines)
        {
            var input = inputs[line.InsumoId];
            var lot = lots[line.LoteId];
            var stockBefore = availableByInput[input.Id];
            var stockAfter = stockBefore - line.CantidadProgramada;
            availableByInput[input.Id] = stockAfter;
            input.StockActual = stockAfter;
            input.StockDisponible = stockAfter;
            _unitOfWork.Repository<Insumo>().Update(input);

            lot.CantidadDisponible -= line.CantidadProgramada;
            _unitOfWork.Repository<Lote>().Update(lot);

            var production = new Produccion
            {
                OrdenProduccion = orderNumber,
                InsumoId = input.Id,
                LoteId = lot.Id,
                FechaInicio = fechaInicio,
                CantidadProgramada = line.CantidadProgramada,
                CantidadConsumida = 0,
                Estado = ProductionStatus.Programada
            };
            await _unitOfWork.Repository<Produccion>().AddAsync(production);
            await _unitOfWork.Repository<MovimientoInventario>().AddAsync(new MovimientoInventario
            {
                InsumoId = input.Id,
                LoteId = lot.Id,
                Produccion = production,
                Tipo = InventoryMovementType.ReservaProduccion,
                Cantidad = line.CantidadProgramada,
                StockAnterior = stockBefore,
                StockPosterior = stockAfter,
                FechaMovimiento = DateTime.UtcNow,
                UsuarioId = usuarioId,
                Referencia = orderNumber,
                Comentario = $"Reserva de {line.CantidadProgramada} para la orden de producción."
            });
            productions.Add(production);
        }

        await _unitOfWork.CommitAsync();
        await _auditService.LogAsync(
            nameof(Produccion),
            productions[0].Id,
            "Crear orden de producción",
            $"Orden de producción {orderNumber} creada con {productions.Count} líneas.",
            usuarioId);

        return productions;
    }

    public async Task<Produccion> IniciarProduccionAsync(int produccionId, int usuarioId)
    {
        var production = await GetProductionAsync(produccionId);
        var orderLines = await GetOrderLinesAsync(production.OrdenProduccion);
        if (orderLines.Any(line => line.Estado != ProductionStatus.Programada))
        {
            throw new BusinessRuleException(
                "Solo se pueden iniciar órdenes de producción completamente programadas.");
        }

        foreach (var line in orderLines)
        {
            await EnsureValidReservationAsync(line);
            line.Estado = ProductionStatus.EnProceso;
            _unitOfWork.Repository<Produccion>().Update(line);
        }

        await _unitOfWork.CommitAsync();
        await _auditService.LogAsync(
            nameof(Produccion),
            production.Id,
            "Iniciar producción",
            $"Orden de producción {production.OrdenProduccion} iniciada.",
            usuarioId);

        return production;
    }

    public async Task<Produccion> FinalizarProduccionAsync(
        int produccionId,
        IReadOnlyDictionary<int, decimal> cantidadesConsumidas,
        int usuarioId)
    {
        ArgumentNullException.ThrowIfNull(cantidadesConsumidas);
        var production = await GetProductionAsync(produccionId);
        var orderLines = await GetOrderLinesAsync(production.OrdenProduccion);
        if (orderLines.Any(line => line.Estado != ProductionStatus.EnProceso))
        {
            throw new BusinessRuleException(
                "La orden debe estar completamente en proceso para finalizarla.");
        }

        if (cantidadesConsumidas.Count != orderLines.Count ||
            orderLines.Any(line => !cantidadesConsumidas.ContainsKey(line.Id)))
        {
            throw new BusinessRuleException(
                "Debes indicar la cantidad consumida de cada insumo de la orden.");
        }

        foreach (var line in orderLines)
        {
            var reserved = await EnsureValidReservationAsync(line);
            var consumed = cantidadesConsumidas[line.Id];
            if (consumed <= 0)
            {
                throw new BusinessRuleException(
                    "La cantidad consumida de cada insumo debe ser mayor que cero.");
            }

            if (consumed > reserved)
            {
                throw new BusinessRuleException(
                    "La cantidad consumida no puede superar lo reservado para cada insumo.");
            }
        }

        var inventory = await LoadOrderInventoryAsync(orderLines);
        foreach (var line in orderLines)
        {
            var inputId = line.InsumoId!.Value;
            var lotId = line.LoteId!.Value;
            var input = inventory.Inputs[inputId];
            var lot = inventory.Lots[lotId];
            var reserved = await GetReservedQuantityAsync(line);
            var consumed = cantidadesConsumidas[line.Id];
            var released = reserved - consumed;
            var stockBefore = inventory.AvailableByInput[inputId];

            lot.CantidadDisponible += released;
            var stockAfter = lot.Bloqueado || lot.EstaVencido
                ? stockBefore
                : stockBefore + released;
            inventory.AvailableByInput[inputId] = stockAfter;
            input.StockActual = stockAfter;
            input.StockDisponible = stockAfter;
            _unitOfWork.Repository<Lote>().Update(lot);
            _unitOfWork.Repository<Insumo>().Update(input);

            line.CantidadConsumida = consumed;
            line.Estado = ProductionStatus.Finalizada;
            line.FechaFin = DateTime.UtcNow;
            _unitOfWork.Repository<Produccion>().Update(line);

            await _unitOfWork.Repository<MovimientoInventario>().AddAsync(new MovimientoInventario
            {
                InsumoId = input.Id,
                LoteId = lot.Id,
                Produccion = line,
                Tipo = InventoryMovementType.Produccion,
                Cantidad = consumed,
                StockAnterior = stockBefore,
                StockPosterior = stockBefore,
                FechaMovimiento = DateTime.UtcNow,
                UsuarioId = usuarioId,
                Referencia = line.OrdenProduccion,
                Comentario = "Consumo de material previamente reservado para producción."
            });

            if (released > 0)
            {
                await _unitOfWork.Repository<MovimientoInventario>().AddAsync(new MovimientoInventario
                {
                    InsumoId = input.Id,
                    LoteId = lot.Id,
                    Produccion = line,
                    Tipo = InventoryMovementType.LiberacionReservaProduccion,
                    Cantidad = released,
                    StockAnterior = stockBefore,
                    StockPosterior = stockAfter,
                    FechaMovimiento = DateTime.UtcNow,
                    UsuarioId = usuarioId,
                    Referencia = line.OrdenProduccion,
                    Comentario = "Liberación de la parte no consumida de la reserva."
                });
            }
        }

        await _unitOfWork.CommitAsync();
        await _auditService.LogAsync(
            nameof(Produccion),
            production.Id,
            "Finalizar producción",
            $"Orden de producción {production.OrdenProduccion} finalizada.",
            usuarioId);

        return production;
    }

    public async Task<Produccion> CancelarProduccionAsync(int produccionId, int usuarioId)
    {
        var production = await GetProductionAsync(produccionId);
        var orderLines = await GetOrderLinesAsync(production.OrdenProduccion);
        if (orderLines.Any(line =>
                line.Estado is not (ProductionStatus.Programada or ProductionStatus.EnProceso)))
        {
            throw new BusinessRuleException(
                "Solo se pueden cancelar órdenes programadas o en proceso.");
        }

        foreach (var line in orderLines)
        {
            await EnsureValidReservationAsync(line);
        }

        var inventory = await LoadOrderInventoryAsync(orderLines);
        foreach (var line in orderLines)
        {
            var inputId = line.InsumoId!.Value;
            var lotId = line.LoteId!.Value;
            var input = inventory.Inputs[inputId];
            var lot = inventory.Lots[lotId];
            var reserved = await GetReservedQuantityAsync(line);
            var stockBefore = inventory.AvailableByInput[inputId];

            lot.CantidadDisponible += reserved;
            var stockAfter = lot.Bloqueado || lot.EstaVencido
                ? stockBefore
                : stockBefore + reserved;
            inventory.AvailableByInput[inputId] = stockAfter;
            input.StockActual = stockAfter;
            input.StockDisponible = stockAfter;
            line.Estado = ProductionStatus.Cancelada;
            line.FechaFin = DateTime.UtcNow;
            _unitOfWork.Repository<Lote>().Update(lot);
            _unitOfWork.Repository<Insumo>().Update(input);
            _unitOfWork.Repository<Produccion>().Update(line);

            await _unitOfWork.Repository<MovimientoInventario>().AddAsync(new MovimientoInventario
            {
                InsumoId = input.Id,
                LoteId = lot.Id,
                Produccion = line,
                Tipo = InventoryMovementType.LiberacionReservaProduccion,
                Cantidad = reserved,
                StockAnterior = stockBefore,
                StockPosterior = stockAfter,
                FechaMovimiento = DateTime.UtcNow,
                UsuarioId = usuarioId,
                Referencia = line.OrdenProduccion,
                Comentario = "Liberación de la reserva por cancelación de la orden."
            });
        }

        await _unitOfWork.CommitAsync();
        await _auditService.LogAsync(
            nameof(Produccion),
            production.Id,
            "Cancelar producción",
            $"Se liberaron las reservas de la orden {production.OrdenProduccion}.",
            usuarioId);

        return production;
    }

    public async Task<IEnumerable<Produccion>> GetHistoricoProduccionAsync(int insumoId)
    {
        return await _unitOfWork.Repository<Produccion>().FindAsync(p => p.InsumoId == insumoId);
    }

    private async Task<string> GenerateOrderNumberAsync()
    {
        var productions = await _unitOfWork.Repository<Produccion>().GetAllAsync();
        var nextNumber = productions
            .Select(production => production.OrdenProduccion)
            .Where(code => code.StartsWith("PROD", StringComparison.OrdinalIgnoreCase))
            .Select(code => int.TryParse(
                code.AsSpan(4),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var number)
                    ? number
                    : 0)
            .DefaultIfEmpty(0)
            .Max();

        string orderNumber;
        do
        {
            nextNumber = checked(nextNumber + 1);
            orderNumber = $"PROD{nextNumber:000}";
        }
        while ((await _unitOfWork.Repository<Produccion>()
                   .FindAsync(p => p.OrdenProduccion == orderNumber))
               .Any());

        return orderNumber;
    }

    private async Task<Produccion> GetProductionAsync(int productionId)
    {
        return await _unitOfWork.Repository<Produccion>().GetByIdAsync(productionId)
            ?? throw new BusinessRuleException("Orden de producción no encontrada.");
    }

    private async Task<List<Produccion>> GetOrderLinesAsync(string orderNumber)
    {
        var lines = (await _unitOfWork.Repository<Produccion>()
                .FindTrackedAsync(line => line.OrdenProduccion == orderNumber))
            .OrderBy(line => line.Id)
            .ToList();
        if (lines.Count == 0)
        {
            throw new BusinessRuleException("No se encontraron líneas para la orden de producción.");
        }

        return lines;
    }

    private async Task<decimal> EnsureValidReservationAsync(Produccion production)
    {
        var reserved = await GetReservedQuantityAsync(production);
        if (reserved != production.CantidadProgramada)
        {
            throw new BusinessRuleException(
                "La orden no tiene una reserva válida registrada. Revise las órdenes antiguas antes de continuar.");
        }

        return reserved;
    }

    private async Task<decimal> GetReservedQuantityAsync(Produccion production)
    {
        var movements = await _unitOfWork.Repository<MovimientoInventario>().FindAsync(m =>
            (m.Tipo == InventoryMovementType.ReservaProduccion ||
             m.Tipo == InventoryMovementType.LiberacionReservaProduccion) &&
            (m.ProduccionId == production.Id || m.Referencia == production.OrdenProduccion));
        var lineMovements = movements.Where(m => m.ProduccionId == production.Id).ToList();
        if (lineMovements.Count > 0)
        {
            return SumReservedMovements(lineMovements);
        }

        return SumReservedMovements(movements.Where(
            m => m.ProduccionId is null && m.Referencia == production.OrdenProduccion));
    }

    private async Task<OrderInventory> LoadOrderInventoryAsync(IReadOnlyList<Produccion> lines)
    {
        var inputs = new Dictionary<int, Insumo>();
        var lots = new Dictionary<int, Lote>();
        foreach (var line in lines)
        {
            var inputId = line.InsumoId
                ?? throw new BusinessRuleException("Una línea de la orden no tiene un insumo asociado.");
            var lotId = line.LoteId
                ?? throw new BusinessRuleException("Una línea de la orden no tiene un lote asociado.");

            if (!inputs.ContainsKey(inputId))
            {
                inputs.Add(inputId, await _unitOfWork.Repository<Insumo>().GetByIdAsync(inputId)
                    ?? throw new BusinessRuleException("No se encontró un insumo asociado a la orden."));
            }

            if (!lots.ContainsKey(lotId))
            {
                lots.Add(lotId, await _unitOfWork.Repository<Lote>().GetByIdAsync(lotId)
                    ?? throw new BusinessRuleException("No se encontró un lote asociado a la orden."));
            }
        }

        var availableByInput = new Dictionary<int, decimal>();
        foreach (var inputId in inputs.Keys)
        {
            availableByInput.Add(
                inputId,
                await _inventoryService.GetAvailableQuantityAsync(inputId));
        }

        return new OrderInventory(inputs, lots, availableByInput);
    }

    private static decimal SumReservedMovements(IEnumerable<MovimientoInventario> movements)
    {
        return movements.Sum(movement =>
            movement.Tipo == InventoryMovementType.ReservaProduccion
                ? movement.Cantidad
                : -movement.Cantidad);
    }

    private sealed record OrderInventory(
        Dictionary<int, Insumo> Inputs,
        Dictionary<int, Lote> Lots,
        Dictionary<int, decimal> AvailableByInput);
}
