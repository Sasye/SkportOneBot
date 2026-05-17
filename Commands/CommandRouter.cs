using System;
using System.Threading.Tasks;
using SkportOneBot.Models;
using SkportOneBot.Commands.Modules;

namespace SkportOneBot.Commands;

public class CommandRouter
{
    private readonly SkportAccountModule _accountModule;
    private readonly EndfieldModule _endfieldModule;

    public CommandRouter(SkportAccountModule accountModule, EndfieldModule endfieldModule)
    {
        _accountModule = accountModule;
        _endfieldModule = endfieldModule;
    }

    public async Task RouteAsync(MessageContext ctx)
    {
        var text = ctx.Text;

        if (text == "终末地绑定" || text == "zmdbd" || text == "skport绑定")
        {
            await _accountModule.BindAsync(ctx);
            return;
        }

        if (text == "绑定列表" || text == "终末地绑定列表" || text == "zmdbdlb")
        {
            await _accountModule.ListBindAsync(ctx);
            return;
        }

        if (text.StartsWith("删除绑定") || text.StartsWith("解绑") || text == "删除全部绑定")
        {
            await _accountModule.DeleteBindAsync(ctx);
            return;
        }

        if (text == "终末地签到" || text == "zmdqd" || text == "skport签到")
        {
            await _endfieldModule.SignAsync(ctx);
            return;
        }
    }
}
