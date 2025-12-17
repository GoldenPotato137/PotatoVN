using Windows.ApplicationModel.Activation;
using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Services;
using GalgameManager.ViewModels;
using Microsoft.Windows.AppLifecycle;

namespace GalgameManager.Activation;

public class UriActivationHandler : ActivationHandler<AppActivationArguments>
{
    private readonly GalgameCollectionService _galgameCollectionService;
    private Galgame? _game;
    private bool _startGame;

    public UriActivationHandler(IGalgameCollectionService galgameCollectionService)
    {
        _galgameCollectionService = (galgameCollectionService as GalgameCollectionService)!;
    }

    protected override bool CanHandleInternal(AppActivationArguments args)
    {
        if (args.Kind != ExtendedActivationKind.Protocol) return false;
        if (args.Data is not ProtocolActivatedEventArgs arg) return false;
        
        // potato-vn://start/{uuid} or potato-vn://view/{uuid}
        var uri = arg.Uri;
        if (uri.Scheme != "potato-vn") return false;

        var segments = uri.Segments; // segments includes slashes, e.g., "/", "start/", "{uuid}"
        if (segments.Length < 2) return false;

        var action = segments[0] == "/" ? segments[1].TrimEnd('/') : segments[0].TrimEnd('/');
        var targetUuid = segments.Length > 2 ? segments[2] : (segments[0] == "/" ? "" : segments[1]); 
        // Logic correction for segments:
        // For potato-vn://start/uuid
        // Segments[0] = "/"
        // Segments[1] = "start/"
        // Segments[2] = "uuid"
        
        // Let's rely on host and local path for easier parsing if standard Uri
        // potato-vn://start/uuid -> Host=start, Path=/uuid
        
        string uuidStr;
        if (uri.Host == "start")
        {
            _startGame = true;
            uuidStr = uri.AbsolutePath.TrimStart('/');
        }
        else if (uri.Host == "view")
        {
            _startGame = false;
            uuidStr = uri.AbsolutePath.TrimStart('/');
        }
        else
        {
            return false;
        }

        try
        {
            _game = _galgameCollectionService.GetGalgameFromUuid(new Guid(uuidStr));
            return _game is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    protected  async override Task HandleInternalAsync(AppActivationArguments args)
    {
        App.GetService<INavigationService>().NavigateTo(typeof(GalgameViewModel).FullName!, new GalgamePageParameter
        {
            Galgame = _game!,
            StartGame = _startGame,
            IsUriLaunch = true
        });
        await Task.CompletedTask;
    }
}
