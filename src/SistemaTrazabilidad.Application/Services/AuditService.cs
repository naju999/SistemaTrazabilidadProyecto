using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SistemaTrazabilidad.Domain.Entities;
using SistemaTrazabilidad.Persistence.Repositories;

namespace SistemaTrazabilidad.Application.Services;

public class AuditService
{
    private readonly IUnitOfWork _unitOfWork;

    public AuditService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Auditoria> LogAsync(string entidad, int entidadId, string accion, string detalle, int? usuarioId = null)
    {
        var auditoria = new Auditoria
        {
            Entidad = entidad,
            EntidadId = entidadId,
            Accion = accion,
            Detalle = detalle,
            UsuarioId = usuarioId,
            FechaHora = DateTime.UtcNow
        };

        await _unitOfWork.Repository<Auditoria>().AddAsync(auditoria);
        await _unitOfWork.CommitAsync();

        return auditoria;
    }

    public async Task<IEnumerable<Auditoria>> GetAuditAsync(string entidad, int entidadId)
    {
        return await _unitOfWork.Repository<Auditoria>().FindAsync(a => a.Entidad == entidad && a.EntidadId == entidadId);
    }
}
