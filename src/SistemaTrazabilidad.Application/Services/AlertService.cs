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

    public AlertService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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
        var insumos = await _unitOfWork.Repository<Insumo>().FindAsync(i => i.StockActual <= i.StockMinimo && i.Activo);
        var alerts = new List<Alerta>();

        foreach (var insumo in insumos)
        {
            var codigo = $"STOCK_MINIMO_{insumo.Id}";
            var mensaje = $"Stock mínimo alcanzado para el insumo {insumo.Nombre}. Stock actual: {insumo.StockActual}.";
            var alerta = await CreateAlertAsync(codigo, mensaje, AlertType.StockMinimo, insumo.Id, null, nameof(Insumo));
            alerts.Add(alerta);
        }

        return alerts;
    }

    public async Task<IEnumerable<Alerta>> GenerateExpiryAlertsAsync(int diasAntesVencimiento = 7)
    {
        var fechaUmbral = DateTime.Today.AddDays(diasAntesVencimiento);
        var lotes = await _unitOfWork.Repository<Lote>().FindAsync(l => !l.Bloqueado && l.FechaVencimiento <= fechaUmbral && l.CantidadDisponible > 0);
        var alerts = new List<Alerta>();

        foreach (var lote in lotes)
        {
            var mensaje = lote.FechaVencimiento.Date < DateTime.Today
                ? $"Lote {lote.CodigoLote} está vencido."
                : $"Lote {lote.CodigoLote} vence el {lote.FechaVencimiento:yyyy-MM-dd}.";
            var codigo = $"VENCIMIENTO_LOTE_{lote.Id}";
            var alerta = await CreateAlertAsync(codigo, mensaje, AlertType.Vencimiento, lote.InsumoId, lote.Id, nameof(Lote));
            alerts.Add(alerta);
        }

        return alerts;
    }

    public async Task<IEnumerable<Alerta>> GetActiveAlertsAsync()
    {
        return await _unitOfWork.Repository<Alerta>().FindAsync(a => a.Activa && !a.Leida);
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
}
