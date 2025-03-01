using LiveshiftCompanion.Utilities;

namespace LiveshiftCompanion.Services;

/// <summary>
/// Modal Error Handler.
/// </summary>
public class ModalErrorHandler : IErrorHandler
{
    private SemaphoreSlim Semaphore { get; } = new(1, 1);

    /// <summary>
    /// Handle error in UI.
    /// </summary>
    /// <param name="ex">Exception.</param>
    public void HandleError(Exception ex)
    {
        DisplayAlert(ex).FireAndForgetSafeAsync();
    }

    private async Task DisplayAlert(Exception ex)
    {
        try
        {
            await Semaphore.WaitAsync();
            if (Shell.Current is { } shell)
                await shell.DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            Semaphore.Release();
        }
    }
}