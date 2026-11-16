using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SistemaTrazabilidad.Application.Exceptions;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Domain.Enums;
using SistemaTrazabilidad.Persistence.Repositories;

namespace SistemaTrazabilidad.Application.Services;

public class SanitaryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AlertService _alertService;
    private readonly AuditService _auditService;

    public SanitaryService(IUnitOfWork unitOfWork, AlertService alertService, AuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _alertService = alertService;
        _auditService = auditService;
    }

    public async Task<RegistroSanitario> RegistrarRegistroSanitarioAsync(int insumoId, DateTime fechaRegistro, decimal temperatura, decimal cloro, decimal ph, decimal madurez, string observaciones, int usuarioId)
    {
        var insumo = await _unitOfWork.Repository<Insumo>().GetByIdAsync(insumoId)
            ?? throw new BusinessRuleException("Insumo no encontrado.");

        if (!insumo.Activo)
        {
            throw new BusinessRuleException("No se pueden registrar controles sanitarios para un insumo inactivo.");
        }

        if (fechaRegistro.Date > DateTime.Today)
        {
            throw new BusinessRuleException("La fecha de registro no puede ser futura.");
        }

        ValidateSanitaryValues(temperatura, cloro, ph, madurez);

        var aprobado = temperatura >= 0 && temperatura <= 40 && cloro >= 0 && cloro <= 5 && ph >= 3.5m && ph <= 10 && madurez >= 0 && madurez <= 100;

        var registro = new RegistroSanitario
        {
            InsumoId = insumoId,
            FechaRegistro = fechaRegistro,
            Temperatura = temperatura,
            Cloro = cloro,
            PH = ph,
            Madurez = madurez,
            Observaciones = observaciones,
            Aprobado = aprobado
        };

        await _unitOfWork.Repository<RegistroSanitario>().AddAsync(registro);
        await _unitOfWork.CommitAsync();

        await _auditService.LogAsync(nameof(RegistroSanitario), registro.Id, "Registrar control sanitario", $"Registro sanitario creado para el insumo {insumo.Nombre}.", usuarioId);

        if (!aprobado)
        {
            await _alertService.CreateAlertAsync(
                $"SANIDAD_{registro.Id}",
                $"Registro sanitario deficiente para el insumo {insumo.Nombre}.",
                AlertType.Sanidad,
                insumoId,
                null,
                nameof(RegistroSanitario));
        }

        return registro;
    }

    private static void ValidateSanitaryValues(decimal temperatura, decimal cloro, decimal ph, decimal madurez)
    {
        if (temperatura < -50 || temperatura > 100)
        {
            throw new BusinessRuleException("La temperatura debe estar entre -50 y 100 grados Celsius.");
        }

        if (cloro < 0 || cloro > 20)
        {
            throw new BusinessRuleException("El valor de cloro debe estar entre 0 y 20 mg/L.");
        }

        if (ph < 0 || ph > 14)
        {
            throw new BusinessRuleException("El valor de pH debe estar entre 0 y 14.");
        }

        if (madurez < 0 || madurez > 100)
        {
            throw new BusinessRuleException("La madurez debe estar entre 0 y 100.");
        }
    }

    public async Task<IEnumerable<RegistroSanitario>> GetHistorialAsync(int insumoId)
    {
        return await _unitOfWork.Repository<RegistroSanitario>().FindAsync(r => r.InsumoId == insumoId);
    }
}
