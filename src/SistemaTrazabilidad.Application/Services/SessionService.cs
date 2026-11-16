using System;
using SistemaTrazabilidad.Domain.Entities;

namespace SistemaTrazabilidad.Application.Services;

public interface ISessionService
{
    Usuario? UsuarioActual { get; }
    bool EstaAutenticado { get; }
    void IniciarSesion(Usuario usuario);
    void CerrarSesion();
    void ValidarSesion();
}

public class SessionService : ISessionService
{
    private Usuario? _usuarioActual;

    public Usuario? UsuarioActual => _usuarioActual;
    public bool EstaAutenticado => _usuarioActual != null;

    public void IniciarSesion(Usuario usuario)
    {
        if (usuario == null)
            throw new ArgumentNullException(nameof(usuario));

        _usuarioActual = usuario;
    }

    public void CerrarSesion()
    {
        _usuarioActual = null;
    }

    public void ValidarSesion()
    {
        if (!EstaAutenticado)
            throw new InvalidOperationException("No hay sesión activa. Debe iniciar sesión primero.");
    }
}
