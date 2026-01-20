using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReptileHunterFaction;

public static class BaseGen_RHFGlobalSettings
{
    public static int prisonsResolved = 0;
    public static int maxPrisons = 0;

    public static void Clear()
    {
        prisonsResolved = 0;
        maxPrisons = 0;
    }
}
