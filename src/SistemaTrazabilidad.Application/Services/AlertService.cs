using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Domain.Enums;
using SistemaTrazabilidad.Persistence.Repositories;

namespace SistemaTrazabilidad.Application.Services;

public class AlertService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly InventoryService _inventoryService;

    public AlertService(IUnitOfWork unitOfWork, InventoryService inventoryService)
    {
        _unitOfWork = unitOfWork;
        _inventoryService = inventoryService;
    }

    public async Task<Alerta> CreateAlertAsync(string codigo, string mensaje, AlertType tipo, int? insumoId = null, int? loteId = null, string entidadRelacionada = "")
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            throw new ArgumentException("El código de alerta es obligatorio.", nameof(codigo));
        }

        if (string.IsNullOrWhiteSpace(mensaje))
        {
            throw new ArgumentException("El mensaje de alerta es obligatorio.", nameof(mensaje));
        }

        var alertaExistente = (await _unitOfWork.Repository<Alerta>().FindAsync(a => a.Codigo == codigo && a.Activa)).FirstOrDefault();
        if (alertaExistente != null)
        {
            return alertaExistente;
        }

        var alerta = new Alerta
        {
            Codigo = codigo,
            Mensaje = mensaje,
            Tipo = tipo,
            InsumoId = insumoId,
            LoteId = loteId,
            FechaGeneracion = DateTime.UtcNow,
            Activa = true,
            EntidadRelacionada = entidadRelacionada
        };

        await _unitOfWork.Repository<Alerta>().AddAsync(alerta);
        await _unitOfWork.CommitAsync();

        return alerta;
    }

    public async Task<IEnumerable<Alerta>> GenerateStockAlertsAsync()
    {
        var insumos = (await _inventoryService.GetStockActualAsync())
            .Where(i => i.StockDisponible <= i.StockMinimo && i.Activo);
        var currentAlerts = insumos
            .Select(insumo => new AlertSnapshot(
                $"STOCK_MINIMO_{insumo.Id}",
                $"Stock mínimo alcanzado para el insumo {insumo.Nombre}. " +
                $"Stock disponible: {insumo.StockDisponible}; mínimo configurado: {insumo.StockMinimo}.",
                AlertType.StockMinimo,
                insumo.Id,
                null,
                nameof(Insumo)))
            .ToList();

        return await SynchronizeAlertsAsync("STOCK_MINIMO_", currentAlerts);
    }

    public async Task<IEnumerable<Alerta>> GenerateExpiryAlertsAsync(int diasAntesVencimiento = 7)
    {
        if (diasAntesVencimiento < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(diasAntesVencimiento),
                "Los días previos al vencimiento no pueden ser negativos.");
        }

        var fechaUmbral = DateTime.Today.AddDays(diasAntesVencimiento);
        var lotes = await _unitOfWork.Repository<Lote>().FindAsync(l =>
            l.InsumoId.HasValue &&
            !l.Bloqueado &&
            l.FechaVencimiento.Date <= fechaUmbral &&
            l.CantidadDisponible > 0);
        var currentAlerts = lotes
            .Select(lote => new AlertSnapshot(
                $"VENCIMIENTO_LOTE_{lote.Id}",
                lote.FechaVencimiento.Date < DateTime.Today
                    ? $"Lote {lote.CodigoInterno} está vencido. " +
                      $"Cantidad disponible: {lote.CantidadDisponible}."
                    : $"Lote {lote.CodigoInterno} vence el {lote.FechaVencimiento:yyyy-MM-dd}. " +
                      $"Cantidad disponible: {lote.CantidadDisponible}.",
                AlertType.Vencimiento,
                lote.InsumoId,
                lote.Id,
                nameof(Lote)))
            .ToList();

        return await SynchronizeAlertsAsync("VENCIMIENTO_LOTE_", currentAlerts);
    }

    public async Task<IReadOnlyList<Alerta>> RefreshCurrentAlertsAsync()
    {
        var stockAlerts = await GenerateStockAlertsAsync();
        var expiryAlerts = await GenerateExpiryAlertsAsync();
        return stockAlerts.Concat(expiryAlerts).ToList();
    }

    public async Task<IEnumerable<Alerta>> GetActiveAlertsAsync()
    {
        return await _unitOfWork.Repository<Alerta>().FindAsync(a => a.Activa);
    }

    public async Task<Alerta> ResolveAlertAsync(int alertaId)
    {
        var alerta = await _unitOfWork.Repository<Alerta>().GetByIdAsync(alertaId)
            ?? throw new InvalidOperationException("Alerta no encontrada.");

        alerta.Activa = false;
        alerta.Leida = true;
        _unitOfWork.Repository<Alerta>().Update(alerta);
        await _unitOfWork.CommitAsync();

        return alerta;
    }

    private async Task<IReadOnlyList<Alerta>> SynchronizeAlertsAsync(
        string codigoPrefix,
        IReadOnlyList<AlertSnapshot> currentConditions)
    {
        var currentByCode = currentConditions.ToDictionary(
            alert => alert.Codigo,
            StringComparer.Ordinal);
        var repository = _unitOfWork.Repository<Alerta>();
        var existingAlerts = (await repository.FindTrackedAsync(
                alert => alert.Codigo.StartsWith(codigoPrefix)))
            .ToList();
        var activeAlerts = new List<Alerta>(currentConditions.Count);
        var hasChanges = false;

        foreach (var condition in currentConditions)
        {
            var matches = existingAlerts
                .Where(alert => alert.Codigo == condition.Codigo)
                .ToList();
            var activeMatches = matches
                .Where(alert => alert.Activa)
                .OrderByDescending(alert => alert.FechaGeneracion)
                .ToList();
            var activeAlert = activeMatches.FirstOrDefault();

            if (activeAlert is null)
            {
                activeAlert = new Alerta
                {
                    Codigo = condition.Codigo,
                    Mensaje = condition.Mensaje,
                    Tipo = condition.Tipo,
                    InsumoId = condition.InsumoId,
                    LoteId = condition.LoteId,
                    FechaGeneracion = DateTime.UtcNow,
                    Activa = true,
                    Leida = false,
                    EntidadRelacionada = condition.EntidadRelacionada
                };
                await repository.AddAsync(activeAlert);
                hasChanges = true;
            }
            else if (activeAlert.Mensaje != condition.Mensaje ||
                     activeAlert.InsumoId != condition.InsumoId ||
                     activeAlert.LoteId != condition.LoteId ||
                     activeAlert.Tipo != condition.Tipo ||
                     activeAlert.EntidadRelacionada != condition.EntidadRelacionada)
            {
                activeAlert.Mensaje = condition.Mensaje;
                activeAlert.InsumoId = condition.InsumoId;
                activeAlert.LoteId = condition.LoteId;
                activeAlert.Tipo = condition.Tipo;
                activeAlert.EntidadRelacionada = condition.EntidadRelacionada;
                activeAlert.FechaGeneracion = DateTime.UtcNow;
                activeAlert.Leida = false;
                repository.Update(activeAlert);
                hasChanges = true;
            }

            foreach (var duplicate in activeMatches.Skip(1))
            {
                duplicate.Activa = false;
                duplicate.Leida = true;
                repository.Update(duplicate);
                hasChanges = true;
            }

            activeAlerts.Add(activeAlert);
        }

        foreach (var staleAlert in existingAlerts.Where(alert =>
                     alert.Activa && !currentByCode.ContainsKey(alert.Codigo)))
        {
            staleAlert.Activa = false;
            staleAlert.Leida = true;
            repository.Update(staleAlert);
            hasChanges = true;
        }

        if (hasChanges)
        {
            await _unitOfWork.CommitAsync();
        }

        return activeAlerts;
    }

    private sealed record AlertSnapshot(
        string Codigo,
        string Mensaje,
        AlertType Tipo,
        int? InsumoId,
        int? LoteId,
        string EntidadRelacionada);
}
