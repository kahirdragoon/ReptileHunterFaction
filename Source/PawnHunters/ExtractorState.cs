using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PawnHunters;
internal enum ExtractorState
{
    Inactive = 0,
    WaitingForOccupant = 1,
    Occupied = 2
}
