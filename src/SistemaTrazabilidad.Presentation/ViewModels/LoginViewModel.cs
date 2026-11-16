using System;
using System.Threading.Tasks;
using System.Windows.Input;
using SistemaTrazabilidad.Application.Services;
using SistemaTrazabilidad.Infrastructure.Security;
using SistemaTrazabilidad.Persistence.Repositories;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SistemaTrazabilidad.Presentation.ViewModels;

public class LoginViewModel : INotifyPropertyChanged
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionService _sessionService;
    private string _username = "";
    private string _password = "";
    private string _mensajeError = "";
    private bool _cargando = false;

    public string Username
    {
        get => _username;
        set
        {
            if (_username != value)
            {
                _username = value;
                OnPropertyChanged();
                MensajeError = ""; // Limpiar error cuando escribe
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (_password != value)
            {
                _password = value;
                OnPropertyChanged();
                MensajeError = "";
            }
        }
    }

    public string MensajeError
    {
        get => _mensajeError;
        set
        {
            if (_mensajeError != value)
            {
                _mensajeError = value;
                OnPropertyChanged();
            }
        }
    }

    public bool Cargando
    {
        get => _cargando;
        set
        {
            if (_cargando != value)
            {
                _cargando = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand LoginCommand { get; }

    public event EventHandler<LoginEventArgs>? LoginExitoso;

    public LoginViewModel(IUnitOfWork unitOfWork, ISessionService sessionService)
    {
        _unitOfWork = unitOfWork;
        _sessionService = sessionService;
        LoginCommand = new AsyncRelayCommand(LoginAsync);
    }

    private async Task LoginAsync()
    {
        if (!ValidarEntrada())
            return;

        Cargando = true;
        try
        {
            var usuarioRepository = _unitOfWork.Repository<Domain.Entities.Usuario>();
            var usuarios = await usuarioRepository.FindAsync(u => u.Username == Username && !u.Bloqueado);
            var usuario = ((System.Collections.Generic.IEnumerable<Domain.Entities.Usuario>)usuarios).FirstOrDefault();

            if (usuario == null)
            {
                MensajeError = "Usuario no encontrado.";
                return;
            }

            if (!PasswordHasher.Verify(Password, usuario.PasswordHash))
            {
                MensajeError = "Contraseña incorrecta.";
                return;
            }

            _sessionService.IniciarSesion(usuario);
            LoginExitoso?.Invoke(this, new LoginEventArgs { Usuario = usuario });
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al iniciar sesión: {ex.Message}";
        }
        finally
        {
            Cargando = false;
        }
    }

    private bool ValidarEntrada()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            MensajeError = "Ingrese un nombre de usuario.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            MensajeError = "Ingrese la contraseña.";
            return false;
        }

        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class LoginEventArgs : EventArgs
{
    public Domain.Entities.Usuario? Usuario { get; set; }
}

public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute?.Invoke() ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        _isExecuting = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

public class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<T?, Task> execute)
    {
        _execute = execute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting;
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        _isExecuting = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            await _execute(parameter is T value ? value : default);
        }
        finally
        {
            _isExecuting = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
