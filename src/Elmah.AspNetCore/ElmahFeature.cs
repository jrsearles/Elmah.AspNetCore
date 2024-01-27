using System;

namespace Elmah.AspNetCore;

internal record ElmahFeature(Guid Id, string Location) : IElmahFeature;
