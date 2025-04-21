using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GalgameManager.Models.BgTasks;

namespace GalgameManager.Models.SteamTasks;

public class SteamSyncTask : BgTaskBase
{
    
    public override string Title => "Steam Sync Task";

    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    protected async override Task RunInternal()
    {
        
    }
}