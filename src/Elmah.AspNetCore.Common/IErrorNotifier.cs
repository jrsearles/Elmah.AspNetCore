using System.Threading.Tasks;

namespace Elmah.AspNetCore;

public interface IErrorNotifier
{
    string Name { get; }

    Task NotifyAsync(Error error);
}