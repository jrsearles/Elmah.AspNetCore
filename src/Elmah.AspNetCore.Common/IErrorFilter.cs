namespace Elmah.AspNetCore;

public interface IErrorFilter
{
    void OnErrorModuleFiltering(object sender, ExceptionFilterEventArgs args);
}